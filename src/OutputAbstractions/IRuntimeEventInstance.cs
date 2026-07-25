namespace SharpSticks.OutputAbstractions;

public interface IRuntimeEventInstance
{
	IInitializedOnAfterRunEvent? BeforeRun(
		RuntimeEventInstance.BeforeRunArgs args,
		CancellationToken cancellationToken = default);
}