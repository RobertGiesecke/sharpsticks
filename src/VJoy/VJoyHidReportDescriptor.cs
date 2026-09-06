using System.Buffers.Binary;
using System.Collections.Immutable;
using Collections.Pooled;

namespace SharpSticks.VJoy;

/// Minimal HID report-descriptor walker for the descriptors vJoy's configuration tool
/// stores per device slot. Reports the input-report axes SharpSticks can feed (Generic
/// Desktop X..Slider2, the same set <see cref="VJoyAxisConstants"/> covers) and the
/// button count. POVs, force-feedback (PID) items and the extended axes of the 2.2.x
/// forks are walked but not reported; the runtime side can't feed them either.
public static class VJoyHidReportDescriptor
{
	private const uint GenericDesktopPage = 0x01;
	private const uint ButtonPage = 0x09;

	/// Guards the usage-range expansion against a corrupt descriptor.
	private const uint MaxUsagesPerItem = 1024;

	private const byte LongItemPrefix = 0xFE;

	private const int MainItemType = 0;
	private const int GlobalItemType = 1;
	private const int LocalItemType = 2;

	private const int InputTag = 8;
	private const int UsagePageTag = 0;
	private const int ReportCountTag = 9;
	private const int PushTag = 10;
	private const int PopTag = 11;
	private const int UsageTag = 0;
	private const int UsageMinimumTag = 1;
	private const int UsageMaximumTag = 2;

	public static bool TryParse(ReadOnlySpan<byte> descriptor, out ImmutableArray<Axis> axes, out uint buttonCount)
	{
		axes = ImmutableArray<Axis>.Empty;
		buttonCount = 0;

		var axesBuilder = ImmutableArray.CreateBuilder<Axis>(8);
		var buttons = 0u;

		// Usages are stored with their page in the high word when the item carried one
		// (4-byte extended form); otherwise the page in effect at the main item applies.
		using var usages = new PooledList<uint>();
		using var globalsStack = new PooledList<(uint UsagePage, uint ReportCount)>();
		var usagePage = 0u;
		var reportCount = 0u;
		var usageMinimum = 0u;
		var usageMaximum = 0u;
		var hasUsageRange = false;

		var offset = 0;
		while (offset < descriptor.Length)
		{
			var prefix = descriptor[offset++];
			if (prefix == LongItemPrefix)
			{
				if (offset + 2 > descriptor.Length)
				{
					return false;
				}

				offset += 2 + descriptor[offset];
				if (offset > descriptor.Length)
				{
					return false;
				}

				continue;
			}

			var size = prefix & 0x03;
			if (size == 3)
			{
				size = 4;
			}

			if (offset + size > descriptor.Length)
			{
				return false;
			}

			var data = ReadUnsigned(descriptor.Slice(offset, size));
			offset += size;

			var type = (prefix >> 2) & 0x03;
			var tag = prefix >> 4;
			switch (type)
			{
				case MainItemType:
					// Input item whose Data/Constant flag (bit 0) says Data.
					if (tag == InputTag && (data & 0x01) == 0)
					{
						CollectInputUsages(
							usages, usagePage, reportCount, usageMinimum, usageMaximum, hasUsageRange,
							axesBuilder, ref buttons);
					}

					usages.Clear();
					hasUsageRange = false;
					break;

				case GlobalItemType:
					switch (tag)
					{
						case UsagePageTag:
							usagePage = data;
							break;
						case ReportCountTag:
							reportCount = data;
							break;
						case PushTag:
							globalsStack.Add((usagePage, reportCount));
							break;
						case PopTag:
							if (globalsStack.Count > 0)
							{
								(usagePage, reportCount) = globalsStack[^1];
								globalsStack.RemoveAt(globalsStack.Count - 1);
							}

							break;
					}

					break;

				case LocalItemType:
					switch (tag)
					{
						case UsageTag:
							usages.Add(data);
							break;
						case UsageMinimumTag:
							usageMinimum = data;
							hasUsageRange = true;
							break;
						case UsageMaximumTag:
							usageMaximum = data;
							hasUsageRange = true;
							break;
					}

					break;
			}
		}

		// Same order the acquiring path (VJoyDeviceFactory.EnumerateAxes) reports.
		axesBuilder.Sort();
		axes = axesBuilder.ToImmutable();
		buttonCount = buttons;
		return true;
	}

	private static void CollectInputUsages(
		PooledList<uint> usages,
		uint usagePage,
		uint reportCount,
		uint usageMinimum,
		uint usageMaximum,
		bool hasUsageRange,
		ImmutableArray<Axis>.Builder axes,
		ref uint buttons)
	{
		var buttonsInItem = 0u;
		foreach (var usage in usages)
		{
			Collect(usage, usagePage, axes, ref buttonsInItem);
		}

		if (hasUsageRange && usageMaximum >= usageMinimum)
		{
			var count = Math.Min(usageMaximum - usageMinimum + 1, MaxUsagesPerItem);
			for (var i = 0u; i < count; i++)
			{
				Collect(usageMinimum + i, usagePage, axes, ref buttonsInItem);
			}
		}

		// A report only has ReportCount fields; usages beyond that don't exist, and the
		// last usage repeating to fill the remaining fields is not an extra button.
		buttons += reportCount > 0 ? Math.Min(buttonsInItem, reportCount) : buttonsInItem;
	}

	private static void Collect(uint usage, uint usagePage, ImmutableArray<Axis>.Builder axes, ref uint buttons)
	{
		var page = usage >> 16;
		if (page == 0)
		{
			page = usagePage;
		}

		var id = usage & 0xFFFF;
		if (page == ButtonPage)
		{
			buttons++;
			return;
		}

		if (page == GenericDesktopPage && VJoyAxisConstants.TryGetAxis(id, out var axis) && !axes.Contains(axis))
		{
			axes.Add(axis);
		}
	}

	private static uint ReadUnsigned(ReadOnlySpan<byte> data) => data.Length switch
	{
		0 => 0,
		1 => data[0],
		2 => BinaryPrimitives.ReadUInt16LittleEndian(data),
		_ => BinaryPrimitives.ReadUInt32LittleEndian(data),
	};
}
