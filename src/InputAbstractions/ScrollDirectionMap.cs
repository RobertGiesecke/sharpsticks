using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>Maps a discrete <see cref="ScrollDirection"/> to a <see cref="ScrollAxis"/> and a signed amount.</summary>
internal static class ScrollDirectionMap
{
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
	public static (ScrollAxis Axis, int Amount) Resolve(ScrollDirection direction, int magnitude) => direction switch
#pragma warning restore CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
	{
		ScrollDirection.Up => (ScrollAxis.Vertical, magnitude),
		ScrollDirection.Down => (ScrollAxis.Vertical, -magnitude),
		ScrollDirection.Right => (ScrollAxis.Horizontal, magnitude),
		ScrollDirection.Left => (ScrollAxis.Horizontal, -magnitude),
	};
}