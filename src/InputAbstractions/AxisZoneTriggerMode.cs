namespace SharpSticks.InputAbstractions;

public enum AxisZoneTriggerMode
{
	/// <summary>Button is held while the axis is in the zone, released when it leaves.</summary>
	Hold,

	/// <summary>
	/// Button is pressed when the axis enters the zone, released after
	/// <see cref="AxisToButtonRoute.PulseDuration"/>, then re-arms once the axis leaves.
	/// </summary>
	Pulse,
}
