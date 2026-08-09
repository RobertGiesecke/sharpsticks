namespace SharpSticks.Overlay.WireProtocol;

/// <summary>
/// Entry point for decoding overlay frames without allocating: tag-checks the
/// frame and hands out the matching reader. All readers are lazy views over
/// the frame bytes — nothing is copied, malformed or truncated input surfaces
/// as a failed <see cref="RefOption{T}"/> / a false <c>MoveNext</c>, never as
/// an out-of-bounds read.
/// </summary>
public readonly ref struct FrameReader
{
	private readonly ReadOnlySpan<byte> _Frame;

	public FrameReader(ReadOnlySpan<byte> frame)
	{
		_Frame = frame;
	}

	public RefOption<FrameType> GetFrameType() => _Frame switch
	{
		[(byte)FrameType.FrameDescriptor, ..] => RefOption.For(FrameType.FrameDescriptor),
		[(byte)FrameType.FrameState, ..] => RefOption.For(FrameType.FrameState),
		_ => new() { Success = false },
	};

	public RefOption<byte> GetVersion() => _Frame.Length switch
	{
		> 1 => new() { Value = _Frame[1] },
		_ => new() { Success = false },
	};

	public RefOption<DescriptorFrameReader> GetDescriptorFrameReader() => GetFrameType() switch
	{
		{ Success: true, Value: FrameType.FrameDescriptor } => RefOption.For(new DescriptorFrameReader(_Frame)),
		_ => new() { Success = false, },
	};

	public RefOption<StateFrameReader> GetStateFrameReader() => GetFrameType() switch
	{
		{ Success: true, Value: FrameType.FrameState } => RefOption.For(new StateFrameReader(_Frame)),
		_ => new() { Success = false, },
	};
}