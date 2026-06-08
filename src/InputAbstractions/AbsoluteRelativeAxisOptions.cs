namespace SharpSticks.InputAbstractions;

/// <summary>
/// Simulates an <em>absolute</em> axis (a position you set directly, like a
/// lever) on top of a <em>relative</em> output (an axis the consumer treats as
/// a velocity: nudge to move, center to hold). The physical input picks a
/// <em>target</em> position; each frame the modifier emits a pulse that nudges
/// the consumer toward that target, and tracks its own belief of where the
/// consumer now is (<c>Current</c>).
///
/// It is a closed loop with two halves you must calibrate <b>to each other</b>:
/// <list type="number">
///   <item><b>The pulse the consumer sees</b> — sized by <see cref="Gain"/>,
///   bounded by <see cref="MinOutput"/>/<see cref="MaxOutput"/>, forced to full
///   at the rails by <see cref="IncreaseEdgeHoldTime"/>/<see cref="DecreaseEdgeHoldTime"/>,
///   and smoothed by <see cref="OutputRiseTime"/>/<see cref="OutputFallTime"/>.</item>
///   <item><b>The internal model</b> — advanced by
///   <see cref="IncreaseTimeToFull"/>/<see cref="DecreaseTimeToFull"/>. When the
///   model reaches the target (within <see cref="ErrorTolerance"/>) it stops
///   pulsing.</item>
/// </list>
/// The cardinal failure mode is a mismatch between the two: if the model
/// advances faster than the consumer actually moves, it declares "arrived" and
/// stops pulsing while the consumer is still short of the target
/// (<b>undershoot</b>); slower, and it keeps pulsing past it (<b>overshoot</b>).
///
/// <para><b>Suggested tuning order</b> (one knob at a time, watch the consumer):
/// <list type="number">
///   <item><see cref="SourceInputMinimum"/>/<see cref="SourceInputMaximum"/> —
///   make full physical travel map to full target travel.</item>
///   <item><see cref="MinOutput"/> — raise until a slow pull reliably moves the
///   consumer (clears its deadzone), no higher.</item>
///   <item><see cref="IncreaseTimeToFull"/>/<see cref="DecreaseTimeToFull"/> —
///   set to the real device's full-travel time at 100% pulse, then nudge until
///   holding the lever steady parks the consumer at the matching spot with no
///   residual undershoot/overshoot. This is the calibration knob.</item>
///   <item><see cref="IncreaseEdgeHoldTime"/>/<see cref="DecreaseEdgeHoldTime"/>
///   — if pinning the lever at a rail still doesn't quite bottom/top the
///   consumer, set this to guarantee a full-drive burst there.</item>
///   <item><see cref="Gain"/> and the slew times — for feel.</item>
/// </list></para>
///
/// Every duration is a <see cref="TimeSpan"/> integrated against wall-clock
/// elapsed time, so behavior is independent of the device's USB report rate and
/// stays stable as that rate drifts.
/// </summary>
public readonly record struct AbsoluteRelativeAxisOptions()
{
	/// <summary>
	/// Output axis pulsed to move the simulated value <em>up</em> (toward
	/// <see cref="Maximum"/>). May be the same binding as
	/// <see cref="DecreaseAxis"/>: the route then becomes a single
	/// bidirectional axis that rests at center, pulsing positive to increase
	/// and negative to decrease (the rest-position options are unused in that
	/// mode).
	/// </summary>
	public required OutputAxisBinding IncreaseAxis { get; init; }

	/// <summary>
	/// Output axis pulsed to move the simulated value <em>down</em> (toward
	/// <see cref="Minimum"/>). <inheritdoc cref="IncreaseAxis"/>
	/// </summary>
	public required OutputAxisBinding DecreaseAxis { get; init; }

	/// <summary>
	/// Physical-input value that maps to <see cref="Minimum"/> (the bottom of
	/// the target range). Input at or below this pins the target to the bottom.
	/// Set it to the lever's <b>resting</b> reading — default <c>0</c> suits an
	/// unsigned axis; a signed lever that rests at <c>-1</c> needs <c>-1</c>
	/// here, otherwise the first half of the pull is dead.
	/// </summary>
	public double SourceInputMinimum { get; init; } = 0.0;

	/// <summary>
	/// Physical-input value that maps to <see cref="Maximum"/> (the top of the
	/// target range). Input at or above this pins the target to the top. Set it
	/// to the lever's <b>fully engaged</b> reading (typically <c>1</c>).
	/// </summary>
	public double SourceInputMaximum { get; init; } = 1.0;

	/// <summary>
	/// Two-axis mode only. The value <see cref="IncreaseAxis"/> emits while
	/// idle, as a <c>[0,1]</c> fraction mapped onto the signed output:
	/// <c>0.5</c> → centered (<c>0</c>), <c>0</c> → <c>-1</c>, <c>1</c> →
	/// <c>+1</c>. Use <c>0.5</c> when the consumer treats a centered axis as
	/// "no movement". Ignored in single-axis (bidirectional) mode.
	/// </summary>
	public double IncreaseRestPosition { get; init; } = 0.5;

	/// <summary>
	/// Two-axis mode only. Idle value for <see cref="DecreaseAxis"/>.
	/// <inheritdoc cref="IncreaseRestPosition"/>
	/// </summary>
	public double DecreaseRestPosition { get; init; } = 0.5;

	/// <summary>
	/// Bottom of the simulated value's range and of the target the input maps
	/// into. The scale is arbitrary but the rate knobs are expressed against
	/// it, so changing it means re-tuning <see cref="IncreaseTimeToFull"/>/
	/// <see cref="DecreaseTimeToFull"/> and <see cref="ErrorTolerance"/>. If
	/// <see cref="Minimum"/> &gt; <see cref="Maximum"/> the two are swapped.
	/// </summary>
	public double Minimum { get; init; } = 0.0;

	/// <summary>Top of the simulated value's range. <inheritdoc cref="Minimum"/></summary>
	public double Maximum { get; init; } = 1.0;

	/// <summary>
	/// The model's assumed starting position, clamped to
	/// <c>[<see cref="Minimum"/>, <see cref="Maximum"/>]</c>. Set it to where
	/// the consumer's value actually sits when the profile starts; if it's
	/// wrong the first moves are off until the model converges.
	/// </summary>
	public double InitialValue { get; init; } = 0.0;

	/// <summary>
	/// Proportional gain: the raw desired pulse is <c>|error| · Gain</c> (before
	/// the <see cref="MinOutput"/>/<see cref="MaxOutput"/> clamp). Higher reaches
	/// <see cref="MaxOutput"/> sooner and corrects harder when far from target;
	/// the pulse saturates while <c>|error| · Gain ≥ MaxOutput</c>, so very high
	/// values lose the proportional ease-in near the target. Too low feels
	/// sluggish and may never exceed <see cref="MinOutput"/>.
	/// </summary>
	public double Gain { get; init; } = 4.0;

	/// <summary>
	/// Upper clamp on pulse magnitude, <c>[0,1]</c> — the strongest the output
	/// ever drives, i.e. the fastest the consumer's value may change. Lower it
	/// to cap slew; <c>1</c> allows full deflection.
	/// </summary>
	public double MaxOutput { get; init; } = 1.0;

	/// <summary>
	/// Lower floor applied to a <em>non-zero</em> pulse: once the modifier
	/// decides to move at all, it emits at least this. The fix for "never
	/// quite reaches the stops" — tiny end-of-travel pulses that fall under the
	/// consumer's input deadzone advance the model but not the consumer, so it
	/// stalls short. Set it just above where the consumer starts reacting; too
	/// high makes the final approach twitchy / overshoot. <c>0</c> = no floor.
	/// </summary>
	public double MinOutput { get; init; }

	/// <summary>
	/// Deadband in target units: when <c>|target − Current| ≤ ErrorTolerance</c>
	/// the modifier stops pulsing and snaps <c>Current</c> to the target. Stops
	/// the loop hunting at rest. Larger = settles sooner but coarser; smaller =
	/// tighter but risks oscillation. Scales with
	/// <see cref="Minimum"/>..<see cref="Maximum"/>.
	/// </summary>
	public double ErrorTolerance { get; init; } = 0.001;

	/// <summary>
	/// While the input is pinned at the <b>top</b> (target = <see cref="Maximum"/>),
	/// drive a full (<see cref="MaxOutput"/>) increase pulse for this long,
	/// regardless of what the model believes. Guarantees the consumer is slammed
	/// all the way to the rail so it mirrors a fully-engaged lever — set it to
	/// roughly the consumer's full-travel time (≥ <see cref="IncreaseTimeToFull"/>)
	/// to be sure. The timer restarts each time the input re-enters the top.
	/// <see cref="TimeSpan.Zero"/> = off.
	/// </summary>
	public TimeSpan IncreaseEdgeHoldTime { get; init; } = TimeSpan.Zero;

	/// <summary>
	/// While the input is pinned at the <b>bottom</b> (target = <see cref="Minimum"/>),
	/// drive a full decrease pulse for this long.
	/// <inheritdoc cref="IncreaseEdgeHoldTime"/>
	/// </summary>
	public TimeSpan DecreaseEdgeHoldTime { get; init; } = TimeSpan.Zero;

	/// <summary>
	/// Time for the emitted pulse to ramp across its full 0→1 magnitude at the
	/// slew limit (wall-clock, frame-rate independent) — smoothing so the output
	/// doesn't snap between pulse levels while rising. Larger = smoother but
	/// laggier onset; <see cref="TimeSpan.Zero"/> (or negative) = instant, no
	/// smoothing.
	/// </summary>
	public TimeSpan OutputRiseTime { get; init; } = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Time for the emitted pulse to ramp across its full range while falling
	/// (e.g. when the target is reached or the direction flips).
	/// <inheritdoc cref="OutputRiseTime"/>
	/// </summary>
	public TimeSpan OutputFallTime { get; init; } = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// <b>The calibration knob.</b> Time the consumer takes to traverse the
	/// <em>whole</em> range (<see cref="Minimum"/>→<see cref="Maximum"/>) at a
	/// full (100%) pulse — i.e. how fast the real device responds. The model
	/// advances <c>Current</c> by <c>pulse · (range / IncreaseTimeToFull) ·
	/// elapsed</c> each frame, so this is wall-clock based and frame-rate
	/// independent: measure it once ("a 100% pulse drives the consumer fully in
	/// ~1 s" → <c>TimeSpan.FromSeconds(1)</c>) and it holds regardless of report
	/// rate. Too small makes the model think it arrived early → stops pulsing →
	/// <b>undershoot</b>; too large lags the consumer → <b>overshoot</b>.
	/// <see cref="TimeSpan.Zero"/> or negative freezes the model (it never
	/// advances — useful for isolating the output pulse).
	/// </summary>
	public TimeSpan IncreaseTimeToFull { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Time to traverse the whole range at full pulse while decreasing.
	/// <inheritdoc cref="IncreaseTimeToFull"/>
	/// </summary>
	public TimeSpan DecreaseTimeToFull { get; init; } = TimeSpan.FromSeconds(1);
}
