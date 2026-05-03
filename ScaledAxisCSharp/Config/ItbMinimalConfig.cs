namespace ScaledAxisCSharp.Config;

public sealed class ItbMinimalConfig
{
	public int VJoyDeviceId { get; set; } = 1;
	public int PollIntervalMs { get; set; } = 5;

	public DeviceAxisSource XAxis { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = "x",
	};

	public DeviceAxisSource YAxis { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = "y",
	};

	public DeviceAxisSource ZAxis { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = "z",
	};

	public DeviceAxisSource ModifierAxis { get; set; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Axis = "slider1",
	};
	public double ModifierMin { get; set; } = -1.0;
	public double ModifierMax { get; set; } = 1.0;

	public double NormalSlope { get; set; } = 1.0;
	public double ModifierPrecisionSlope { get; set; } = 0.184;
	public double HoldPrecisionSlope { get; set; } = 0.508;

	public bool EnableAxis5XOverride { get; set; }
	public DeviceAxisSource Axis5OverrideAxis { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Axis = "ry",
	};
	public double Axis5OverrideDeadzone { get; set; } = 0.05;

	public DeviceButtonSource PrimaryFireButton { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Button = 1,
	};

	public DeviceButtonSource LeftPrimaryButton { get; set; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Button = 1,
	};

	public DeviceButtonSource LeftAuxButton { get; set; } = new()
	{
		DeviceName = "LEFT VPC Stick WarBRD",
		Button = 11,
	};

	public DeviceButtonSource SecondaryFireButton { get; set; } = new()
	{
		DeviceName = "RIGHT VPC Stick WarBRD",
		Button = 18,
	};

	public List<DeviceButtonSource> PrecisionButtons { get; set; } =
	[
		new DeviceButtonSource
		{
			DeviceName = "LEFT VPC Stick WarBRD",
			Button = 2,
		},
		new DeviceButtonSource
		{
			DeviceName = "RIGHT VPC Stick WarBRD",
			Button = 2,
		},
		new DeviceButtonSource
		{
			DeviceName = "RIGHT VPC Stick WarBRD",
			Button = 16,
		}
	];

	public int PulseMs { get; set; } = 50;

	public void Validate()
	{
		RequireAxisSource(XAxis, nameof(XAxis));
		RequireAxisSource(YAxis, nameof(YAxis));
		RequireAxisSource(ZAxis, nameof(ZAxis));
		RequireAxisSource(ModifierAxis, nameof(ModifierAxis));
		RequireAxisSource(Axis5OverrideAxis, nameof(Axis5OverrideAxis));
		RequireButtonSource(PrimaryFireButton, nameof(PrimaryFireButton));
		RequireButtonSource(LeftPrimaryButton, nameof(LeftPrimaryButton));
		RequireButtonSource(LeftAuxButton, nameof(LeftAuxButton));
		RequireButtonSource(SecondaryFireButton, nameof(SecondaryFireButton));

		if (PrecisionButtons is null)
		{
			throw new InvalidOperationException($"{nameof(PrecisionButtons)} cannot be null.");
		}

		for (var index = 0; index < PrecisionButtons.Count; index++)
		{
			RequireButtonSource(PrecisionButtons[index], $"{nameof(PrecisionButtons)}[{index}]");
		}
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

		if (string.IsNullOrWhiteSpace(source.Axis))
		{
			throw new InvalidOperationException($"{propertyName}.Axis is required.");
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