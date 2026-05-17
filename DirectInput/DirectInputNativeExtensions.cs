namespace ScaledAxisCSharp.DirectInput;

internal static class DirectInputNativeExtensions
{
	extension(DirectInputNative)
	{
		public static Guid GuidXAxis => Axis.GuidXAxis;
		public static Guid GuidYAxis => Axis.GuidYAxis;
		public static Guid GuidZAxis => Axis.GuidZAxis;
		public static Guid GuidRxAxis => Axis.GuidRxAxis;
		public static Guid GuidRyAxis => Axis.GuidRyAxis;
		public static Guid GuidRzAxis => Axis.GuidRzAxis;
		public static Guid GuidSlider => Axis.GuidSlider;
	}
}