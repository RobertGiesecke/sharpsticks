using ScaledAxisCSharp.InputAbstractions;

namespace ScaledAxisCSharp.DirectInput;

public static class DirectInputPhysicalAxisExtensions
{
	public static readonly Guid XAxisGuid = new(0xA36D02E0, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid YAxisGuid = new(0xA36D02E1, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid ZAxisGuid = new(0xA36D02E2, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid RxAxisGuid = new(0xA36D02F4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid RyAxisGuid = new(0xA36D02F5, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid RzAxisGuid = new(0xA36D02E3, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	public static readonly Guid SliderGuid = new(0xA36D02E4, 0xC9F3, 0x11CF, 0xBF, 0xC7, 0x44, 0x45, 0x53, 0x54,
		0x00,
		0x00);

	extension(PhysicalAxis)
	{
		public static Guid GuidXAxis => XAxisGuid;
		public static Guid GuidYAxis => YAxisGuid;
		public static Guid GuidZAxis => ZAxisGuid;
		public static Guid GuidRxAxis => RxAxisGuid;
		public static Guid GuidRyAxis => RyAxisGuid;
		public static Guid GuidRzAxis => RzAxisGuid;
		public static Guid GuidSlider => SliderGuid;

		public static PhysicalAxis? GetDirectInputPhysicalAxis(Guid axisGuid, ref int sliderIndex)
		{
			if (axisGuid == XAxisGuid)
			{
				return PhysicalAxis.X;
			}

			if (axisGuid == YAxisGuid)
			{
				return PhysicalAxis.Y;
			}

			if (axisGuid == ZAxisGuid)
			{
				return PhysicalAxis.Z;
			}

			if (axisGuid == RxAxisGuid)
			{
				return PhysicalAxis.Rx;
			}

			if (axisGuid == RyAxisGuid)
			{
				return PhysicalAxis.Ry;
			}

			if (axisGuid == RzAxisGuid)
			{
				return PhysicalAxis.Rz;
			}

			if (axisGuid == SliderGuid && sliderIndex < 2)
			{
				var axis = sliderIndex == 0 ? PhysicalAxis.Slider1 : PhysicalAxis.Slider2;
				sliderIndex++;
				return axis;
			}

			return null;
		}
	}
}