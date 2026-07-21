namespace Namestnik.Core.Models;

public enum GemColor
{
	Blue,
	Red,
	Green,
	Yellow
}

public static class GemColorExtensions
{
	public static GemColor Parse(string value) => value.ToLowerInvariant() switch
	{
		"blue" => GemColor.Blue,
		"red" => GemColor.Red,
		"green" => GemColor.Green,
		"yellow" => GemColor.Yellow,
		_ => throw new ArgumentException($"Unknown gem color: {value}")
	};

	public static string ToJson(this GemColor color) => color.ToString().ToLowerInvariant();
}
