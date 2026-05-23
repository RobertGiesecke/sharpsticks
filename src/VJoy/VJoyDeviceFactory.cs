using Collections.Pooled;

namespace SharpSticks.VJoy;

public sealed class VJoyDeviceFactory : IOutputDeviceFactory
{
	public static VJoyDeviceFactory Instance { get; } = new();

	/// vJoy's HID device exposes VID 0x1234 / PID 0xBEAD. Every vJoy device on Windows
	/// surfaces under the same ProductGuid; we disambiguate which DirectInput entry
	/// corresponds to which vJoy slot by (axis count, button count) fingerprint and
	/// stable sequential claim from the candidate pool.
	internal static Guid VJoyProductGuid { get; } = ProductGuidEncoder.Encode(vendor: 0x1234, product: 0xBEAD);

	PooledList<OutputDevice> IOutputDeviceFactory.Open(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice> availableInputs)
	{
		var devices = new PooledList<OutputDevice>(requests.Count);
		try
		{
			OpenAll(requests, availableInputs, devices);
			return devices;
		}
		catch
		{
			DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}

	/// Public convenience overload for callers (tests, examples) that work directly with
	/// concrete <see cref="VJoyDevice"/> instances.
	public PooledList<VJoyDevice> Open(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice>? availableInputs = null)
	{
		var devices = new PooledList<VJoyDevice>(requests.Count);
		try
		{
			OpenAll(requests, availableInputs, devices);
			return devices;
		}
		catch
		{
			DisposeAll(devices);
			devices.Dispose();
			throw;
		}
	}

	private static void OpenAll<TDevice>(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice>? availableInputs,
		PooledList<TDevice> destination)
		where TDevice : OutputDevice
	{
		VJoyNative.EnsureLoaded();
		if (!VJoyNative.VJoyEnabled())
		{
			throw new InvalidOperationException(
				"vJoy is not enabled. Install and configure the vJoy driver first.");
		}

		// Pre-filter candidate inputs to vJoy-only entries, sorted by DeviceId. We claim
		// from the front sequentially as we walk the (also DeviceId-sorted) requests, so
		// non-contiguous slot ids (e.g. 1 and 5) still pair to the 1st and 2nd
		// DirectInput-side vJoy entries.
		using var candidatePool = BuildCandidatePool(availableInputs);

		foreach (var request in requests.OrderBy(static r => r.DeviceId))
		{
			var (device, caps) = OpenOne(request);
			device.InputDeviceId = ClaimMatchingInput(caps, candidatePool);
			destination.Add((TDevice)(OutputDevice)device);
		}
	}

	private static PooledList<JoystickDevice> BuildCandidatePool(IReadOnlyList<JoystickDevice>? availableInputs)
	{
		var pool = new PooledList<JoystickDevice>(availableInputs?.Count ?? 0);
		if (availableInputs is null)
		{
			return pool;
		}

		foreach (var input in availableInputs)
		{
			if (input.ProductGuid == VJoyProductGuid)
			{
				pool.Add(input);
			}
		}

		pool.Sort(static (a, b) => a.DeviceId.CompareTo(b.DeviceId));
		return pool;
	}

	private static (VJoyDevice Device, JoystickCapabilities Capabilities) OpenOne(OutputDeviceRequest request)
	{
		var deviceId = request.DeviceId;
		if (deviceId < 1)
		{
			throw new InvalidOperationException("vJoy device ids are 1-based.");
		}

		var status = VJoyNative.GetVJDStatus(deviceId);
		if (status == VjdStatus.Busy)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is already in use by another feeder.");
		}

		if (status == VjdStatus.Missing)
		{
			throw new InvalidOperationException($"vJoy device {deviceId} is not configured.");
		}

		if (!VJoyNative.AcquireVJD(deviceId))
		{
			throw new InvalidOperationException(
				$"Failed to acquire vJoy device {deviceId}. Current status: {status}.");
		}

		if (!VJoyNative.ResetVJD(deviceId))
		{
			VJoyNative.RelinquishVJD(deviceId);
			throw new InvalidOperationException($"Failed to reset vJoy device {deviceId}.");
		}

		try
		{
			using var axisLimits = new PooledDictionary<Axis, AxisLimits>();
			foreach (var axis in request.AxisRoutes.Select(static route => route.OutputBinding.Axis).Distinct())
			{
				var hidUsage = axis.GetVJoyAxisId();
				if (!VJoyNative.GetVJDAxisExist(deviceId, hidUsage))
				{
					throw new InvalidOperationException($"Axis '{axis}' is not enabled on vJoy device {deviceId}.");
				}

				var min = 0;
				var max = 0;
				if (!VJoyNative.GetVJDAxisMin(deviceId, hidUsage, ref min) ||
				    !VJoyNative.GetVJDAxisMax(deviceId, hidUsage, ref max))
				{
					throw new InvalidOperationException($"Failed reading limits for vJoy axis '{axis}'.");
				}

				axisLimits.Add(axis, new(min, max));
			}

			var buttonCount = VJoyNative.GetVJDButtonNumber(deviceId);
			foreach (var targetButton in request.ButtonRoutes
				         .Select(static route => route.OutputBinding.ButtonNumber)
				         .Concat(request.MacroButtonNumbers)
				         .Distinct())
			{
				if (targetButton > buttonCount)
				{
					throw new InvalidOperationException(
						$"Button {targetButton} is not enabled on vJoy device {deviceId}. Device exposes {buttonCount} buttons.");
				}
			}

			var device = new VJoyDevice(deviceId, axisLimits.ToFrozenDictionary());
			var caps = new JoystickCapabilities(
				NumAxes: (uint)axisLimits.Count,
				NumButtons: (uint)Math.Max(0, buttonCount),
				NumPovs: 0);
			return (device, caps);
		}
		catch
		{
			VJoyNative.RelinquishVJD(deviceId);
			throw;
		}
	}

	/// Walk the (already filtered + sorted) pool front-to-back, claim the first entry
	/// whose <see cref="JoystickCapabilities"/> matches this output's caps, remove it.
	/// Returns null when no candidate is left or none matches the caps fingerprint.
	private static int? ClaimMatchingInput(JoystickCapabilities caps, PooledList<JoystickDevice> candidatePool)
	{
		for (var i = 0; i < candidatePool.Count; i++)
		{
			if (candidatePool[i].Capabilities == caps)
			{
				var deviceId = candidatePool[i].DeviceId;
				candidatePool.RemoveAt(i);
				return deviceId;
			}
		}

		return null;
	}

	private static void DisposeAll<TDevice>(PooledList<TDevice> devices) where TDevice : OutputDevice
	{
		foreach (var device in devices)
		{
			device.Dispose();
		}
	}
}
