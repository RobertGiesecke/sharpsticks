namespace SharpSticks.Tests;

/// <summary>
/// The run-event builders and their state threading: BeforeRun hands back an
/// after-run handle only when it produced state, the handle carries the
/// runtime, the state, and the run outcome flags into OnAfterRun, and the
/// untyped <see cref="IRuntimeEventInstance"/> bridge recovers the typed
/// runtime context.
/// </summary>
public sealed class RunEventInstanceTests : IDisposable
{
	private readonly FakeDeviceManager _Fakes = new();
	private readonly IFakesOutputRuntimeContext _Runtime;

	public RunEventInstanceTests()
	{
		var stick = _Fakes.AddInputDevice("Stick").AddAxis(Axis.X).Build();
		_Fakes.AddOutputDevice().AddAxis(Axis.X).Build();
		_Runtime = _Fakes.BuildRuntime(new()
		{
			Name = "test",
			ConnectedDevices = _Fakes.InputDevices,
			Routes = [],
		});
	}

	public void Dispose()
	{
		_Runtime.Dispose();
		_Fakes.Dispose();
	}

	[Fact]
	public void WithState_ThreadsRuntimeStateAndOutcome_FromBeforeToAfter()
	{
		object? beforeRuntime = null;
		object? afterRuntime = null;
		string? afterState = null;
		var afterOutcome = (Started: false, Failed: true);

		IRuntimeEventInstance instance = _Runtime
			.NewRunEvent((args, _) =>
			{
				beforeRuntime = args.Runtime;
				return "the-state";
			})
			.WithAfterRun((args, _) =>
			{
				afterRuntime = args.Runtime;
				afterState = args.State;
				afterOutcome = (args.RunStarted, args.RunFailed);
			});

		var handle = instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken);
		Assert.NotNull(handle);
		Assert.Same(_Runtime, beforeRuntime);

		handle!.OnAfterRun(new() { RunStarted = true, RunFailed = false }, TestContext.Current.CancellationToken);
		Assert.Same(_Runtime, afterRuntime);
		Assert.Equal("the-state", afterState);
		Assert.Equal((true, false), afterOutcome);
	}

	[Fact]
	public void NullStateFromBeforeRun_YieldsNoAfterRunHandle()
	{
		var afterRan = false;

		IRuntimeEventInstance instance = _Runtime
			.NewRunEvent((_, _) => (string?)null)
			.WithAfterRun((_, _) => afterRan = true);

		Assert.Null(instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken));
		Assert.False(afterRan);
	}

	[Fact]
	public void NoStateVariant_RunsBefore_AndStillGetsAnAfterRunHandle()
	{
		var beforeRan = false;
		var afterRan = false;

		IRuntimeEventInstance instance = _Runtime
			.NewRunEvent((_, _) => { beforeRan = true; })
			.WithAfterRun((_, _) => afterRan = true);

		var handle = instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken);
		Assert.True(beforeRan);
		Assert.NotNull(handle);

		handle!.OnAfterRun(new() { RunStarted = true, RunFailed = false }, TestContext.Current.CancellationToken);
		Assert.True(afterRan);
	}

	[Fact]
	public void NoAfterRun_RunsBefore_AndYieldsNoHandle()
	{
		var beforeRan = false;

		IRuntimeEventInstance instance = _Runtime
			.NewRunEvent((_, _) => { beforeRan = true; })
			.NoAfterRun();

		// Nothing to unwind → no handle for the run loop to track.
		Assert.Null(instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken));
		Assert.True(beforeRan);
	}

	[Fact]
	public void RunEventSetup_WithoutState_WiresBothCallbacks()
	{
		var order = new List<string>();

		IRuntimeEventInstance instance = _Runtime.NewRunEvent()
			.WithoutState(
				(_, _) => order.Add("before"),
				(_, _) => order.Add("after"));

		var handle = instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken);
		handle!.OnAfterRun(new() { RunStarted = true, RunFailed = false }, TestContext.Current.CancellationToken);

		Assert.Equal(["before", "after"], order);
	}

	[Fact]
	public void RunEventSetup_WithState_ThreadsTheState()
	{
		string? seen = null;

		IRuntimeEventInstance instance = _Runtime.NewRunEvent()
			.WithState(
				(_, _) => "threaded",
				(args, _) => seen = args.State);

		instance.BeforeRun(new() { Runtime = _Runtime }, TestContext.Current.CancellationToken)!
			.OnAfterRun(new() { RunStarted = true, RunFailed = false }, TestContext.Current.CancellationToken);

		Assert.Equal("threaded", seen);
	}
}
