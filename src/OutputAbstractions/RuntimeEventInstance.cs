namespace SharpSticks.OutputAbstractions;

public static class RuntimeEventInstance
{
	public readonly record struct BeforeRunArgs
	{
		public required IRuntimeContext Runtime { get; init; }
	}

	public readonly record struct BeforeRunArgs<TInputDevice, TOutputDevice>
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public required IOutputRuntimeContext<TInputDevice, TOutputDevice> Runtime { get; init; }
	}

	public readonly record struct AfterRunArgs<TState>
	{
		public required IRuntimeContext Runtime { get; init; }
		public required TState State { get; init; }
	}

	public readonly record struct AfterRunArgs<TInputDevice, TOutputDevice, TState>
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public required bool RunStarted { get; init; }
		public required bool RunFailed { get; init; }

		public required IOutputRuntimeContext<TInputDevice, TOutputDevice> Runtime { get; init; }
		public required TState State { get; init; }
	}


	extension<TInputDevice, TOutputDevice>(ICombinedDeviceFactory<TInputDevice, TOutputDevice> factory)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public static RunEventSetup<TInputDevice, TOutputDevice> NewRunEvent() => new();
	}

	extension<TInputDevice, TOutputDevice>(IOutputRuntimeContext<TInputDevice, TOutputDevice> runtimeContext)
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public RunEventSetup<TInputDevice, TOutputDevice> NewRunEvent() => new();

		public RunEventSetup<TInputDevice, TOutputDevice>.WithBeforeRunSetup<TState> NewRunEvent<TState>(
			Func<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken, TState?> onBeforeRun)
			where TState : class =>
			new(onBeforeRun);

		public RunEventSetup<TInputDevice, TOutputDevice>.WithBeforeRunSetupNoState NewRunEvent(
			Action<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken> onBeforeRun) =>
			new(onBeforeRun);
	}

	public sealed class NoState
	{
		public static NoState Instance { get; } = new();

		private NoState()
		{
		}
	};

	public readonly record struct RunEventSetup<TInputDevice, TOutputDevice>
		where TInputDevice : JoystickDevice
		where TOutputDevice : OutputDevice
	{
		public RuntimeEventInstance<TInputDevice, TOutputDevice, NoState> WithoutState(
			Action<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken> onBeforeRun,
			Action<AfterRunArgs<TInputDevice, TOutputDevice, NoState>, CancellationToken> onAfterRun
		) =>
			new()
			{
				OnBeforeRun = (args, ct) =>
				{
					onBeforeRun(args, ct);
					return NoState.Instance;
				},
				OnAfterRun = onAfterRun,
			};

		public RuntimeEventInstance<TInputDevice, TOutputDevice, TState> WithState<TState>(
			Func<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken, TState?> onBeforeRun,
			Action<AfterRunArgs<TInputDevice, TOutputDevice, TState>, CancellationToken> onAfterRun
		) where TState : class => new()
		{
			OnBeforeRun = onBeforeRun,
			OnAfterRun = onAfterRun,
		};

		public readonly record struct WithBeforeRunSetup<TState>
			where TState : class
		{
			private readonly Func<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken, TState?> _OnBeforeRun;

			public WithBeforeRunSetup(
				Func<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken, TState?> onBeforeRun)
			{
				_OnBeforeRun = onBeforeRun;
			}

			public RuntimeEventInstance<TInputDevice, TOutputDevice, TState> NoAfterRun() => new()
			{
				OnBeforeRun = _OnBeforeRun,
				OnAfterRun = null,
			};

			public RuntimeEventInstance<TInputDevice, TOutputDevice, TState> WithAfterRun(
				Action<AfterRunArgs<TInputDevice, TOutputDevice, TState>, CancellationToken> onAfterRun) => new()
			{
				OnBeforeRun = _OnBeforeRun,
				OnAfterRun = onAfterRun,
			};
		}

		public readonly record struct WithBeforeRunSetupNoState
		{
			private readonly Action<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken> _OnBeforeRun;

			public WithBeforeRunSetupNoState(
				Action<BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken> onBeforeRun)
			{
				_OnBeforeRun = onBeforeRun;
			}

			public RuntimeEventInstance<TInputDevice, TOutputDevice, NoState> NoAfterRun()
			{
				var onBeforeRun = _OnBeforeRun;
				return new()
				{
					OnBeforeRun = (args, token) =>
					{
						onBeforeRun(new()
						{
							Runtime = args.Runtime,
						}, token);

						return NoState.Instance;
					},
					OnAfterRun = null,
				};
			}

			public RuntimeEventInstance<TInputDevice, TOutputDevice, NoState> WithAfterRun(
				Action<AfterRunArgs<TInputDevice, TOutputDevice, NoState>, CancellationToken> onAfterRun)
			{
				var onBeforeRun = _OnBeforeRun;
				return new()
				{
					OnBeforeRun = (args, token) =>
					{
						onBeforeRun(new()
						{
							Runtime = args.Runtime,
						}, token);

						return NoState.Instance;
					},
					OnAfterRun = onAfterRun,
				};
			}
		}
	}
}

public sealed class RuntimeEventInstance<TInputDevice, TOutputDevice, TState> : IRuntimeEventInstance
	where TInputDevice : JoystickDevice
	where TOutputDevice : OutputDevice
	where TState : class?
{
	// A handle exists only when BeforeRun produced state AND an after-run was
	// wired — gating on the state TYPE would silently disable WithAfterRun on
	// the no-state variants (NoState.Instance is real state).
	public IInitializedOnAfterRunEvent? BeforeRun(
		RuntimeEventInstance.BeforeRunArgs<TInputDevice, TOutputDevice> args,
		CancellationToken cancellationToken = default) =>
		OnBeforeRun(args, cancellationToken) switch
		{
#pragma warning disable CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
			{ } state when OnAfterRun is not null => new InitializedOnAfterRunEvent<TInputDevice, TOutputDevice, TState>()
#pragma warning restore CS8634 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'class' constraint.
			{
				Self = this,
				Runtime = args.Runtime,
				State = state,
			},
			_ => null,
		};

	public required Func<RuntimeEventInstance.BeforeRunArgs<TInputDevice, TOutputDevice>, CancellationToken, TState?>
		OnBeforeRun { get; init; }

	public required Action<RuntimeEventInstance.AfterRunArgs<TInputDevice, TOutputDevice, TState>, CancellationToken>?
		OnAfterRun { get; init; }


	IInitializedOnAfterRunEvent? IRuntimeEventInstance.BeforeRun(
		RuntimeEventInstance.BeforeRunArgs args,
		CancellationToken cancellationToken) => BeforeRun(new()
	{
		Runtime = (IOutputRuntimeContext<TInputDevice, TOutputDevice>)args.Runtime,
	}, cancellationToken);
}

internal readonly record struct InitializedOnAfterRunEvent<TInputDevice, TOutputDevice, TState>
	: IInitializedOnAfterRunEvent
	where TInputDevice : JoystickDevice
	where TOutputDevice : OutputDevice
	where TState : class
{
	public required RuntimeEventInstance<TInputDevice, TOutputDevice, TState> Self { get; init; }
	public required IOutputRuntimeContext<TInputDevice, TOutputDevice> Runtime { get; init; }
	public required TState State { get; init; }

	public void OnAfterRun(AfterRunArgs args, CancellationToken cancellationToken)
	{
		// The handle is only created when an after-run was wired (see BeforeRun).
		Self.OnAfterRun!(new()
		{
			RunStarted = args.RunStarted,
			RunFailed = args.RunFailed,
			Runtime = Runtime,
			State = State,
		}, cancellationToken);
	}
}