using System.Reflection;
using SharpSticks.InputSynthesis.Keyboard;
using SharpSticks.InputSynthesis.Mouse;

namespace SharpSticks.Tests;

/// <summary>
/// The <see cref="KeyboardOutput.NamedKeys"/> and <see cref="MouseOutput.Buttons"/>
/// catalogs are hand-written tables — sweep them by reflection so a copy-paste
/// slip (two entries bound to the same key) can't hide.
/// </summary>
public sealed class NamedKeyCatalogTests
{
	[Fact]
	public void NamedKeys_AreNonEmpty_AndAllDistinct()
	{
		var entries = typeof(KeyboardOutput.NamedKeys)
			.GetNestedTypes(BindingFlags.Public)
			.SelectMany(group => group
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(field => field.FieldType == typeof(KeyTarget))
				.Select(field => (
					Entry: $"{group.Name}.{field.Name}",
					((KeyTarget)field.GetValue(null)!).Key)))
			.ToArray();

		Assert.NotEmpty(entries);

		var duplicates = entries
			.GroupBy(e => e.Key)
			.Where(g => g.Count() > 1)
			.Select(g => string.Join(" = ", g.Select(e => e.Entry)))
			.ToArray();
		Assert.Empty(duplicates);
	}

	[Fact]
	public void FromNamedKey_AcceptsDefinedKeys_AndRejectsUndefinedOnes()
	{
		Assert.Equal((Key)NamedKey.A, KeyboardOutput.FromNamedKey(NamedKey.A));
		Assert.Throws<ArgumentException>(() => KeyboardOutput.FromNamedKey((NamedKey)int.MaxValue));
	}

	[Fact]
	public void MouseButtons_CoverEveryOutputButton_Distinctly()
	{
		var buttons = typeof(MouseOutput.Buttons)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.FieldType == typeof(MouseButtonTarget))
			.Select(field => ((MouseButtonTarget)field.GetValue(null)!).Button)
			.ToArray();

		Assert.Equal(buttons.Length, buttons.Distinct().Count());
		Assert.Equal(
			Enum.GetValues<OutputMouseButton>().OrderBy(b => b),
			buttons.OrderBy(b => b));
	}
}
