namespace SharpSticks.InputAbstractions;

public sealed record AxisBindingWithModifier : IAxisModifier, IMergeableObject<AxisBindingWithModifier>
{
	public required AxisBinding SourceAxis { get; init; }
	public required IAxisModifier? Modifier { get; init; }

	IRuntimeAxisModifier IModifier<IRuntimeAxisModifier>.CreateModifierRuntimeContext<TInputDevice>(
		IRuntimeContext<TInputDevice> context)
	{
		var runtimeAxisModifier = SourceAxis.CreateModifierRuntimeContext(context);
		if (Modifier is null)
		{
			return runtimeAxisModifier;
		}

		var modifierRuntime = Modifier.CreateModifierRuntimeContext(context);
		return new AxisValueReader(runtimeAxisModifier, modifierRuntime);
	}

	void IFillDevices.FillDevices(ICollection<int> deviceIds)
	{
		SourceAxis.FillDevices(deviceIds);
		Modifier?.FillDevices(deviceIds);
	}

	private sealed class AxisValueReader : IRuntimeAxisModifier
	{
		private readonly IRuntimeAxisModifier _AxisRuntime;
		private readonly IRuntimeAxisModifier _RuntimeAxisModifier;

		public AxisValueReader(
			IRuntimeAxisModifier axisRuntime,
			IRuntimeAxisModifier runtimeAxisModifier)
		{
			_AxisRuntime = axisRuntime;
			_RuntimeAxisModifier = runtimeAxisModifier;
		}

		public double Apply(double input, JoystickState?[] states, ApplyMode applyMode = ApplyMode.Update)
		{
			var value = _AxisRuntime.Apply(input, states, applyMode);
			return _RuntimeAxisModifier.Apply(value, states, applyMode);
		}
	}

	public AxisBindingWithModifier Merge(MergeObjectContext context)
	{
		var hasChanges = false;
		var x1 = this.SourceAxis.MergeOrGet(context, ref hasChanges);
		var x2 = Modifier?.MergeOrGet(context, ref hasChanges);

		return !hasChanges
			? this
			// ReSharper disable once WithExpressionModifiesAllMembers
			: this with
			{
				SourceAxis = x1,
				Modifier = x2,
			};
	}
}