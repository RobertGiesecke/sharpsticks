namespace ScaledAxisCSharp.DirectInput;

internal static class DirectInputNativeExtensions
{
	extension(DirectInputNative)
	{
		public static Guid GuidXAxis => PhysicalAxis.GuidXAxis;
		public static Guid GuidYAxis => PhysicalAxis.GuidYAxis;
		public static Guid GuidZAxis => PhysicalAxis.GuidZAxis;
		public static Guid GuidRxAxis => PhysicalAxis.GuidRxAxis;
		public static Guid GuidRyAxis => PhysicalAxis.GuidRyAxis;
		public static Guid GuidRzAxis => PhysicalAxis.GuidRzAxis;
		public static Guid GuidSlider => PhysicalAxis.GuidSlider;
	}
}