namespace SharpSticks.Overlay.WireProtocol;

public static class RefOption
{
	public static RefOption<T> None<T>()
		where T : allows ref struct
		=> new() { Success = false };

	public static RefOption<T> For<T>(T value)
		where T : allows ref struct
		=> new() { Value = value };
}

/// <summary>
/// A success-or-nothing wrapper usable with ref struct payloads (the frame
/// readers/writers). <see cref="Value"/> throws when <see cref="Success"/> is
/// false — pattern-match (<c>is { Success: true } option</c>) to branch.
/// </summary>
public readonly ref struct RefOption<T>
	where T : allows ref struct
{
	public bool Success { get; init; }

	public static implicit operator RefOption<T>(T value) => new() { Value = value };

	public T Value
	{
		get => Success switch
		{
			false => throw new InvalidOperationException("Option does not contain a value."),
			_ => field,
		};
		init
		{
			field = value;
			Success = true;
		}
	}
}
