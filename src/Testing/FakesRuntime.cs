using System.Collections.Frozen;

namespace SharpSticks.Testing;

public static class FakesRuntime
{
	public static IFakesOutputRuntimeContext Build(
		RuntimeBuilder.BuildOptions<FakeJoystickDevice, FakeOutputDevice> options)
	{
		var timeSource = options.TimeSource switch
		{
			FakeTimeSource t => t,
			null => new(),
			var wrongTimeSource => throw new ArgumentException(
				$"{nameof(options)}.{nameof(options.TimeSource)} must be null or a {nameof(FakeTimeSource)}, found {wrongTimeSource.GetType().FullName}",
				nameof(options)),
		};

		var runtimeContext = Runtime<FakeJoystickDevice, FakeOutputDevice>.Build(options with
		{
			TimeSource = timeSource,
		});
		return new FakesOutputRuntimeContext(runtimeContext, timeSource);
	}

	private sealed class FakesOutputRuntimeContext : IFakesOutputRuntimeContext
	{
		private readonly IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice> _Implementation;
		public FakeTimeSource TimeSource { get; }


		public FakesOutputRuntimeContext(IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice> implementation,
			FakeTimeSource fakeTimeSource)
		{
			_Implementation = implementation;
			TimeSource = fakeTimeSource;
		}


		FrozenDictionary<int, int> IRuntimeContext.DeviceIndexesById => _Implementation.DeviceIndexesById;

		OutputButtonStateIndex? IRuntimeContext.TryGetOutputStateIndex(OutputButtonBinding binding) =>
			_Implementation.TryGetOutputStateIndex(binding);

		ITimeSource IRuntimeContext.TimeSource => _Implementation.TimeSource;

		IInputSynthesizer? IRuntimeContext.InputSynthesizer => _Implementation.InputSynthesizer;


		FrozenDictionary<int, FakeJoystickDevice> IRuntimeContext<FakeJoystickDevice>.DevicesById =>
			_Implementation.DevicesById;

		ImmutableArray<FakeJoystickDevice> IRuntimeContext<FakeJoystickDevice>.Devices => _Implementation.Devices;

		void IDisposable.Dispose() => _Implementation.Dispose();

		string IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice>.Name => _Implementation.Name;

		ImmutableArray<FakeOutputDevice> IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice>.OutputDevices =>
			_Implementation.OutputDevices;

		void IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice>.Run(CancellationToken cancellationToken,
			DebugLogger? debugLogger) =>
			_Implementation.Run(cancellationToken, debugLogger);

		void IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice>.ProcessFrame(DebugLogger? debugLogger) =>
			_Implementation.ProcessFrame(debugLogger);

		int IOutputRuntimeContext<FakeJoystickDevice, FakeOutputDevice>.ComputeWaitTimeoutMs() =>
			_Implementation.ComputeWaitTimeoutMs();
	}
}