namespace SharpSticks.Shared;

public static class MergeableObjectExtensions
{
	[OverloadResolutionPriority(1)]
	public static T MergeOrGet<T>(this T instance, MergeObjectContext context) =>
		instance switch
		{
			IMergeableObject<T> x => context.GetMerged(x),
			IMergeableObject x => (T)context.GetMerged(x),
			_ => context.GetMerged(instance),
		};

	[OverloadResolutionPriority(3)]
	public static T MergeOrGet<T>(this T instance, MergeObjectContext context,
		ref bool hasChanges)
	{
		var result = MergeOrGet(instance, context);
		if (hasChanges)
		{
			return result;
		}

		hasChanges = !ReferenceEquals(instance, result);
		return result;
	}

	public readonly record struct MergeOrGetAllOptions
	{
		public bool ReturnUniqueItems { get; init; }
	}

	[OverloadResolutionPriority(1)]
	public static ImmutableArray<T> MergeOrGetAll<T>(
		this ImmutableArray<T> instances,
		MergeOrGetAllOptions? options = null)
	{
		if (instances.IsDefaultOrEmpty)
		{
			return instances;
		}

		using var mergedObjectsLookup =
			new PooledDictionary<IMergeableObject, IMergeableObject>(instances.Length);
		var mergeObjectContext = new MergeObjectContext
		{
			MergedObjects = mergedObjectsLookup,
		};

		return MergeOrGetAll(instances, mergeObjectContext, options);
	}

	[OverloadResolutionPriority(1)]
	public static ImmutableArray<T> MergeOrGetAll<T>(
		this ImmutableArray<T> instances,
		MergeObjectContext context,
		MergeOrGetAllOptions? options = null)
	{
		var hasChanges = false;
		return instances.MergeOrGetAll(context, ref hasChanges, options);
	}

	[OverloadResolutionPriority(1)]
	public static ImmutableArray<T> MergeOrGetAll<T>(
		this ImmutableArray<T> instances,
		MergeObjectContext context,
		ref bool hasChanges,
		MergeOrGetAllOptions? options = null)
	{
		if (instances.IsDefaultOrEmpty)
		{
			return instances;
		}

		var usedOptions = options ?? new MergeOrGetAllOptions();

		using var newInstances = new PooledList<T>(instances.Length);
		using var newInstancesSet = usedOptions.ReturnUniqueItems ? new PooledSet<T>(instances.Length) : null;

		{
			foreach (var instance in instances)
			{
				var singleAxisChanged = false;
				var x3 = instance.MergeOrGet(context, ref singleAxisChanged);
				if (newInstancesSet?.Add(x3) is false)
				{
					hasChanges = true;
					continue;
				}

				newInstances.Add(x3);

				if (!hasChanges && singleAxisChanged)
				{
					hasChanges = true;
				}
			}
		}

		return !hasChanges ? instances : [..newInstances.Span];
	}
}