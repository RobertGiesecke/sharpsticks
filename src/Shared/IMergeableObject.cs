using System.Runtime.CompilerServices;

namespace SharpSticks.Shared;

/// <summary>
/// Can be merged to a single instance based on equality
/// </summary>
public interface IMergeableObject
{
	/// <summary>
	/// Merge all instances of this object and its properties so only the first occurence is used
	/// </summary>
	/// <param name="context"></param>
	/// <returns></returns>
	[OverloadResolutionPriority(0)]
	IMergeableObject Merge(MergeObjectContext context);
};

public interface IMergeableObject<out T> : IMergeableObject
{
	/// <inheritdoc cref="IMergeableObject.Merge"/>
	[OverloadResolutionPriority(3)]
	new T Merge(MergeObjectContext context);

	IMergeableObject IMergeableObject.Merge(MergeObjectContext context) => (IMergeableObject?)Merge(context) ??
	                                                                       throw new InvalidOperationException(
		                                                                       $"{nameof(Merge)} failed");
}

public readonly record struct MergeObjectContext
{
	public required IDictionary<IMergeableObject, IMergeableObject> MergedObjects { get; init; }

	[OverloadResolutionPriority(2)]
	public T GetMerged<T>(IMergeableObject<T> mergeableObject)
	{
		if (MergedObjects.TryGetValue(mergeableObject, out var existingMergedObject))
		{
			return (T)existingMergedObject;
		}

		var result = mergeableObject.Merge(this);

		MergedObjects.Add(
			mergeableObject,
			(IMergeableObject?)result ??
			throw new InvalidOperationException($"{nameof(mergeableObject.Merge)} failed"));
		return result;
	}

	[OverloadResolutionPriority(1)]
	public T GetMerged<T>(T instance)
	{
		if (instance is not IMergeableObject mergeableObject)
		{
			return instance;
		}


		if (MergedObjects.TryGetValue(mergeableObject, out var existingMergedObject))
		{
			return (T)existingMergedObject;
		}

		var result = mergeableObject switch
		{
			IMergeableObject<T> m => m.Merge(this),
			_ => (T)mergeableObject.Merge(this),
		};

		MergedObjects.Add(mergeableObject,
			(IMergeableObject?)result ??
			throw new InvalidOperationException($"{nameof(mergeableObject.Merge)} failed"));
		return result;
	}
}