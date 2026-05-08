using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.VJoy;

public static class VJoyAxisConstants
{
	public const uint X = 0x30;
	public const uint Y = 0x31;
	public const uint Z = 0x32;
	public const uint Rx = 0x33;
	public const uint Ry = 0x34;
	public const uint Rz = 0x35;
	public const uint Slider1 = 0x36;
	public const uint Slider2 = 0x37;
}

public static class VJoyAxisExtensions
{
	extension(PhysicalAxis address)
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public uint GetVJoyAxisId() => address switch
		{
			PhysicalAxis.X => VJoyAxisConstants.X,
			PhysicalAxis.Y => VJoyAxisConstants.Y,
			PhysicalAxis.Z => VJoyAxisConstants.Z,
			PhysicalAxis.Rx => VJoyAxisConstants.Rx,
			PhysicalAxis.Ry => VJoyAxisConstants.Ry,
			PhysicalAxis.Rz => VJoyAxisConstants.Rz,
			PhysicalAxis.Slider1 => VJoyAxisConstants.Slider1,
			PhysicalAxis.Slider2 => VJoyAxisConstants.Slider2,
			_ => throw new ArgumentOutOfRangeException(nameof(address), address, null),
		};
	}
}