using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.OutputAbstractions;

public static class MouseOutput
{
	public static class Directions
	{
		public static readonly MouseDirection Horizontal = MouseDirection.X;
		public static readonly MouseDirection Vertical = MouseDirection.Y;
	}

	public static class ScrollWheel
	{
		public static readonly ScrollTarget Horizontal = new() { Axis = ScrollAxis.Horizontal, };
		public static readonly ScrollTarget Vertical = new() { Axis = ScrollAxis.Vertical, };
	}

	public static class Buttons
	{
		public static MouseButtonTarget Left = new() { Button = OutputMouseButton.Left };
		public static MouseButtonTarget Right = new() { Button = OutputMouseButton.Right };
		public static MouseButtonTarget Middle = new() { Button = OutputMouseButton.Middle };
		public static MouseButtonTarget X1 = new() { Button = OutputMouseButton.X1 };
		public static MouseButtonTarget X2 = new() { Button = OutputMouseButton.X2 };
	}
}