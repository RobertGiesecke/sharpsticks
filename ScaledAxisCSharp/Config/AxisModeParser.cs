namespace ScaledAxisCSharp.Config;

internal static class AxisModeParser
{
	public static AxisMode Parse(string value)
	{
		if (string.Equals(value, "signed", StringComparison.OrdinalIgnoreCase))
		{
			return AxisMode.Signed;
		}

		if (string.Equals(value, "unsigned", StringComparison.OrdinalIgnoreCase))
		{
			return AxisMode.Unsigned;
		}

		throw new InvalidOperationException($"Unsupported axis mode '{value}'. Use 'signed' or 'unsigned'.");
	}
}
