using SharpSticks.InputSynthesis.Keyboard;

namespace SharpSticks.InputAbstractions;

/// <summary>
/// A keyboard key as a <see cref="ButtonTarget"/>: held down while asserted (a key tap
/// when driven by a Pulse zone). <see cref="Key"/> accepts <see cref="NamedKey"/> implicitly.
/// </summary>
public sealed record KeyTarget(Key Key) : ButtonTarget<KeyTarget>
{
	public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
		new KeySink(RequireSynthesizer(context), Key);

	protected override KeyTarget Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = Key.MergeOrGet(context, ref hasChanged);

		return !hasChanged
			? this
			// ReSharper disable once WithExpressionModifiesAllMembers
			: this with
			{
				Key = x1,
			};
	}
}