using System.Runtime.CompilerServices;

namespace ScaledAxisCSharp.VJoy;

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