using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.InputAbstractions;

/// <summary>A synthesized mouse button as a <see cref="ButtonTarget"/>.</summary>
public sealed record MouseButtonTarget : ButtonTarget<MouseButtonTarget>
{
	public required OutputMouseButton Button { get; init; }

	public override IButtonStateSink CreateRuntimeSink(IButtonSinkContext context) =>
		new MouseButtonSink(RequireSynthesizer(context), Button);

	protected override MouseButtonTarget Merge(MergeObjectContext context)
	{
		var hasChanged = false;
		var x1 = Button.MergeOrGet(context, ref hasChanged);

		return !hasChanged
			? this
			// ReSharper disable once WithExpressionModifiesAllMembers
			: this with
			{
				Button = x1,
			};
	}
}