using System.Collections.Immutable;
using Collections.Pooled;

namespace SharpSticks.VJoy;

public sealed class VJoyDeviceFactory : IOutputDeviceFactory<VJoyDevice>
{
	public static VJoyDeviceFactory Instance { get; } = new();

	/// vJoy's HID device exposes VID 0x1234 / PID 0xBEAD. Every vJoy device on Windows
	/// surfaces under the same ProductGuid; we disambiguate which DirectInput entry
	/// corresponds to which vJoy slot by (axis count, button count) fingerprint and
	/// stable sequential claim from the candidate pool.
	internal static Guid VJoyProductGuid { get; } = VirtualOutputProducts.VJoyProductGuid;

	/// The name vJoy's DirectInput entries report; used so a declared output maps to the same
	/// identity as its input-side counterpart.
	internal const string VJoyProductName = "vJoy Device";

	public IInputSynthesizer? InputSynthesizer => WindowsInputSynthesizer.Instance;

	public AvailableOutputDevice DescribeDeclaredOutput(uint deviceId, ImmutableArray<Axis> axes, uint buttonCount) =>
		new(deviceId, axes, buttonCount, VJoyProductGuid, VJoyProductName);

	/// Design-time snapshot of the configured vJoy slots. Deliberately bypasses
	/// vJoyInterface.dll: its capability queries leave a device handle open for the life of
	/// the calling process, so from a compiler server / IDE they lock the slot against the
	/// actual feeder. See <see cref="VJoyConfigurationReader"/>.
	public ImmutableArray<AvailableOutputDevice> EnumerateAvailableOutputs() =>
		VJoyConfigurationReader.EnumerateConfiguredDevices();

	private static ImmutableArray<Axis> EnumerateAxes(uint deviceId)
	{
		var builder = ImmutableArray.CreateBuilder<Axis>();
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x30)) builder.Add(Axis.X);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x31)) builder.Add(Axis.Y);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x32)) builder.Add(Axis.Z);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x33)) builder.Add(Axis.Rx);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x34)) builder.Add(Axis.Ry);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x35)) builder.Add(Axis.Rz);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x36)) builder.Add(Axis.Slider1);
		if (VJoyNative.GetVJDAxisExist(deviceId, 0x37)) builder.Add(Axis.Slider2);
		return builder.ToImmutable();
	}

	/// Public convenience overload for callers (tests, examples) that work directly with
	/// concrete <see cref="VJoyDevice"/> instances.
	public PooledList<VJoyDevice> EnumerateConnectedOutputDevices(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<JoystickDevice>? availableInputs = null)
	{
		using var devices = new PooledList<VJoyDevice>(requests.Count).DeferList();
		OpenAll(requests, availableInputs, devices.List);
		return devices.GetAndSkipDispose();
	}

	private static void OpenAll<TInputDevice, TDevice>(
		IReadOnlyCollection<OutputDeviceRequest> requests,
		IReadOnlyList<TInputDevice>? availableInputs,
		PooledList<TDevice> destination)
		where TInputDevice : JoystickDevice
		where TDevice : OutputDevice
	{
		VJoyNative.EnsureLoaded();
		if (!VJoyNative.VJoyEnabled())
		{
			throw new InvalidOperationException("vJoy is not enabled. Install and configure the vJoy driver first.");
		}

		// Pre-filter candidate inputs to vJoy-only entries, sorted by DeviceId. We claim
		// from the front sequentially as we walk the (also DeviceId-sorted) requests, so
		// non-contiguous slot ids (e.g. 1 and 5) still pair to the 1st and 2nd
		// DirectInput-side vJoy entries.
		using var candidatePool = BuildCandidatePool(availableInputs);

		foreach (var request in requests.OrderBy(static r => r.DeviceId))
		{
			var device = OpenOne(request, candidatePool);
			destination.Add((TDevice)(OutputDevice)device);
		}
	}

	private static PooledList<TInputDevice> BuildCandidatePool<TInputDevice>(
		IReadOnlyList<TInputDevice>? availableInputs)
		where TInputDevice : JoystickDevice
	{
		var pool = new PooledList<TInputDevice>(availableInputs?.Count ?? 0);
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

	private static VJoyDevice OpenOne<TInputDevice>(
		OutputDeviceRequest request,
		PooledList<TInputDevice> candidatePool)
		where TInputDevice : JoystickDevice
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
			throw new InvalidOperationException($"Failed to acquire vJoy device {deviceId}. Current status: {status}.");
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
			foreach (var targetButton in request.OutputButtons
				         .Select(static button => button.ButtonNumber)
				         .Concat(request.MacroButtonNumbers)
				         .Distinct())
			{
				if (targetButton > buttonCount)
				{
					throw new InvalidOperationException(
						$"Button {targetButton} is not enabled on vJoy device {deviceId}. Device exposes {buttonCount} buttons."
					);
				}
			}

			// Match against the DirectInput entry's *full* capabilities, not the routed
			// subset. DirectInput reports every enabled axis/button/POV, so using only the
			// routed axis count (axisLimits.Count) here means the fingerprint never matched
			// and no input device was paired.
			var povCount = Math.Max(0, VJoyNative.GetVJDContPovNumber(deviceId)) +
			               Math.Max(0, VJoyNative.GetVJDDiscPovNumber(deviceId));
			var caps = new JoystickCapabilities(
				NumAxes: (uint)EnumerateAxes(deviceId).Length,
				NumButtons: (uint)Math.Max(0, buttonCount),
				NumPovs: (uint)povCount
			);
			var inputDeviceId = ClaimMatchingInput(caps, candidatePool);
			return new VJoyDevice(deviceId, axisLimits.ToFrozenDictionary(), inputDeviceId);
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
	private static int? ClaimMatchingInput<TInputDevice>(JoystickCapabilities caps,
		PooledList<TInputDevice> candidatePool)
		where TInputDevice : JoystickDevice
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