namespace ScaledAxisCSharp.OutputAbstractions;

public interface IOutputRuntimeContext : IRuntimeContext, IDisposable
{
	string Name { get; }
	ImmutableArray<OutputDevice> OutputDevices { get; }
	void Run(CancellationToken cancellationToken, DebugLogger? debugLogger = null);
}