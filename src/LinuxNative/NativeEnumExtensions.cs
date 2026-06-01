namespace SharpSticks.LinuxNative;

/// Enum → native ABI value conversions for the cases where a native argument is *shared* across
/// several value kinds, so it can't simply be typed as one enum.
///
/// Dedicated args that obviously take a single enum (e.g. <c>open</c>'s flags, <c>epoll_ctl</c>'s op)
/// are just typed as that enum on the P/Invoke and need no conversion. <see cref="EvType"/> is the
/// exception: the generic <see cref="LinuxLibc.IoctlInt"/> value (UI_SET_EVBIT / -KEYBIT / -ABSBIT)
/// and the <c>EVIOCGBIT</c> request encoder also carry raw button/abs codes, so the enum converts here.
public static class NativeEnumExtensions
{
	/// EV_* type as its <c>__u16</c> value — widens implicitly to the int/uint those callers take.
	public static ushort ToNative(this EvType type) => (ushort)type;
}
