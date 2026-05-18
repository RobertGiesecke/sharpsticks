using System.Runtime.CompilerServices;

namespace SharpSticks.VJoy;

public static class VJoyAxisExtensions
{
	extension(Axis address)
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public uint GetVJoyAxisId() => address switch
		{
			Axis.X => VJoyAxisConstants.X,
			Axis.Y => VJoyAxisConstants.Y,
			Axis.Z => VJoyAxisConstants.Z,
			Axis.Rx => VJoyAxisConstants.Rx,
			Axis.Ry => VJoyAxisConstants.Ry,
			Axis.Rz => VJoyAxisConstants.Rz,
			Axis.Slider1 => VJoyAxisConstants.Slider1,
			Axis.Slider2 => VJoyAxisConstants.Slider2,
			_ => throw new ArgumentOutOfRangeException(nameof(address), address, null),
		};
	}
}