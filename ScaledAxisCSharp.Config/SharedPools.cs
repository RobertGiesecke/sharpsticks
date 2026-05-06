using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace ScaledAxisCSharp.Config;

public static class SharedPools
{
	private static readonly DefaultObjectPool<StringBuilder> StringBuilderPool = new(new StringBuilderPooledObjectPolicy());

	public static readonly ObjectPoolWrapper<StringBuilder> StringBuilder = new(StringBuilderPool);

	public sealed class ObjectPoolWrapper<T> where T : class
	{
		private readonly ObjectPool<T> _ObjectPool;

		public ObjectPoolWrapper(ObjectPool<T> objectPool)
		{
			_ObjectPool = objectPool ?? throw new ArgumentNullException(nameof(objectPool));
		}

		internal void Return(T instance) => _ObjectPool.Return(instance);

		public ObjectScope<T> GetInstance()
		{
			return new ObjectScope<T>
			{
				Instance = _ObjectPool.Get(),
			};
		}
	}

	public readonly record struct ObjectScope<T> : IDisposable
		where T : class
	{
		private readonly ObjectPoolWrapper<T> _ObjectPoolWrapper;

		internal ObjectScope(ObjectPoolWrapper<T> objectPoolWrapper)
		{
			_ObjectPoolWrapper = objectPoolWrapper;
		}

		public required T Instance { get; init; }

		public void Dispose()
		{
			_ObjectPoolWrapper.Return(Instance);
		}
	}
}