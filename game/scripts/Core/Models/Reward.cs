using System.Text.Json;
using System.Text.Json.Serialization;

namespace Namestnik.Core.Models;

[JsonConverter(typeof(RewardJsonConverter))]
public abstract record Reward;

public sealed record VpReward(int Amount) : Reward;
public sealed record GemsReward(int Amount) : Reward;
public sealed record CardReward(int Amount) : Reward;
public sealed record ScienceReward(int Amount) : Reward;
public sealed record MagicReward(int Amount) : Reward;
public sealed record DefenseReward(int Amount) : Reward;
public sealed record AttackReward(int Amount) : Reward;
public sealed record InfiniteReward(GemColor Color) : Reward;
public sealed record BonusMagicReward(int Amount) : Reward;
public sealed record BonusCircleReward(GemColor Color, int Amount) : Reward;
public sealed record MultiReward(IReadOnlyList<Reward> Parts) : Reward;
public sealed record ChoiceReward(IReadOnlyList<Reward> Options) : Reward;

public sealed class RewardJsonConverter : JsonConverter<Reward>
{
	public override Reward Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using var doc = JsonDocument.ParseValue(ref reader);
		return Parse(doc.RootElement, options);
	}

	public override void Write(Utf8JsonWriter writer, Reward value, JsonSerializerOptions options)
	{
		throw new NotSupportedException("Reward serialization is not required at runtime.");
	}

	public static Reward Parse(JsonElement el, JsonSerializerOptions options)
	{
		var type = el.GetProperty("type").GetString()
			?? throw new JsonException("Reward missing type");

		return type switch
		{
			"vp" => new VpReward(el.GetProperty("amount").GetInt32()),
			"gems" => new GemsReward(el.GetProperty("amount").GetInt32()),
			"card" => new CardReward(el.GetProperty("amount").GetInt32()),
			"science" => new ScienceReward(el.GetProperty("amount").GetInt32()),
			"magic" => new MagicReward(el.GetProperty("amount").GetInt32()),
			"defense" => new DefenseReward(el.GetProperty("amount").GetInt32()),
			"attack" => new AttackReward(el.GetProperty("amount").GetInt32()),
			"infinite" => new InfiniteReward(GemColorExtensions.Parse(el.GetProperty("color").GetString()!)),
			"bonus_magic" => new BonusMagicReward(el.GetProperty("amount").GetInt32()),
			"bonus_circle" => new BonusCircleReward(
				GemColorExtensions.Parse(el.GetProperty("color").GetString()!),
				el.GetProperty("amount").GetInt32()),
			"multi" => new MultiReward(ParseList(el.GetProperty("parts"), options)),
			"choice" => new ChoiceReward(ParseList(el.GetProperty("options"), options)),
			_ => throw new JsonException($"Unknown reward type: {type}")
		};
	}

	static List<Reward> ParseList(JsonElement arr, JsonSerializerOptions options)
	{
		var list = new List<Reward>();
		foreach (var item in arr.EnumerateArray())
			list.Add(Parse(item, options));
		return list;
	}
}
