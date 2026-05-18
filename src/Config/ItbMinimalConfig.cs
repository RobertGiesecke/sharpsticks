namespace SharpSticks.Config;

public sealed record ItbMinimalConfig
{
	public int VJoyDeviceId { get; init; } = 1;
	public int PollIntervalMs { get; init; } = 5;

	public DeviceAxisSource XAxis { get; init; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = Axis.X,
	};

	public DeviceAxisSource YAxis { get; init; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = Axis.Y,
	};

	public DeviceAxisSource ZAxis { get; init; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = Axis.Z,
	};

	public DeviceAxisSource ModifierAxis { get; init; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Axis = Axis.Slider1,
	};

	public double ModifierMin { get; init; } = -1.0;
	public double ModifierMax { get; init; } = 1.0;

	public double NormalSlope { get; init; } = 1.0;
	public double ModifierPrecisionSlope { get; init; } = 0.184;
	public double HoldPrecisionSlope { get; init; } = 0.508;

	public DeviceButtonSource PrimaryFireButton { get; init; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Button = 1,
	};

	public DeviceButtonSource LeftPrimaryButton { get; init; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Button = 1,
	};

	public DeviceButtonSource LeftAuxButton { get; init; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Button = 11,
	};

	public DeviceButtonSource SecondaryFireButton { get; init; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Button = 18,
	};

	public List<DeviceButtonSource> PrecisionButtons { get; init; } =
	[
		new()
		{
			DeviceName = "LEFT VPC Stick WarBRD",
			Button = 2,
		},
		new()
		{
			DeviceName = "RIGHT VPC Stick WarBRD",
			Button = 2,
		},
		new()
		{
			DeviceName = "RIGHT VPC Stick WarBRD",
			Button = 16,
		},
	];

	public int PulseMs { get; init; } = 50;

	public void Validate()
	{
		RequireAxisSource(XAxis, nameof(XAxis));
		RequireAxisSource(YAxis, nameof(YAxis));
		RequireAxisSource(ZAxis, nameof(ZAxis));
		RequireAxisSource(ModifierAxis, nameof(ModifierAxis));
		RequireButtonSource(PrimaryFireButton, nameof(PrimaryFireButton));
		RequireButtonSource(LeftPrimaryButton, nameof(LeftPrimaryButton));
		RequireButtonSource(LeftAuxButton, nameof(LeftAuxButton));
		RequireButtonSource(SecondaryFireButton, nameof(SecondaryFireButton));

		if (PrecisionButtons is null)
		{
			throw new InvalidOperationException($"{nameof(PrecisionButtons)} cannot be null.");
		}

		for (var index = 0; index < PrecisionButtons.Count; index++)
			RequireButtonSource(PrecisionButtons[index], $"{nameof(PrecisionButtons)}[{index}]");
	}

	private static void RequireAxisSource(DeviceAxisSource? source, string propertyName)
	{
		if (source is null)
		{
			throw new InvalidOperationException($"{propertyName} cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(source.DeviceName))
		{
			throw new InvalidOperationException($"{propertyName}.DeviceName is required.");
		}
	}

	private static void RequireButtonSource(DeviceButtonSource? source, string propertyName)
	{
		if (source is null)
		{
			throw new InvalidOperationException($"{propertyName} cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(source.DeviceName))
		{
			throw new InvalidOperationException($"{propertyName}.DeviceName is required.");
		}

		if (source.Button < 1)
		{
			throw new InvalidOperationException($"{propertyName}.Button must be at least 1.");
		}
	}
}