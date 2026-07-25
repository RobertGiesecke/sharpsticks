namespace SharpSticks.OutputAbstractions;

public readonly record struct AfterRunArgs
{
	public required bool RunStarted { get; init; }
	public required bool RunFailed { get; init; }
}

public interface IInitializedOnAfterRunEvent
{
	void OnAfterRun(AfterRunArgs args, CancellationToken cancellationToken = default);
}