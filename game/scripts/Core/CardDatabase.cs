using System.Text.Json;
using Namestnik.Core.Models;

namespace Namestnik.Core;

/// <summary>Loads and indexes card definitions from cards.json.</summary>
public sealed class CardDatabase
{
	public IReadOnlyDictionary<int, CharacterCard> Characters { get; }
	public IReadOnlyDictionary<int, LawCard> Laws { get; }

	CardDatabase(
		IReadOnlyDictionary<int, CharacterCard> characters,
		IReadOnlyDictionary<int, LawCard> laws)
	{
		Characters = characters;
		Laws = laws;
	}

	public static CardDatabase LoadFromFile(string path) =>
		LoadFromJson(File.ReadAllText(path));

	public static CardDatabase LoadFromJson(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		var characters = new Dictionary<int, CharacterCard>();
		foreach (var el in root.GetProperty("characters").EnumerateArray())
		{
			var card = ParseCharacter(el);
			characters[card.Id] = card;
		}

		var laws = new Dictionary<int, LawCard>();
		foreach (var el in root.GetProperty("laws").EnumerateArray())
		{
			var card = ParseLaw(el);
			laws[card.Id] = card;
		}

		return new CardDatabase(characters, laws);
	}

	public CharacterCard GetCharacter(int id) => Characters[id];
	public LawCard GetLaw(int id) => Laws[id];

	static CharacterCard ParseCharacter(JsonElement el)
	{
		var levels = new List<LevelReward>();
		foreach (var lv in el.GetProperty("levels").EnumerateArray())
		{
			levels.Add(new LevelReward
			{
				Level = lv.GetProperty("level").GetInt32(),
				Cost = GemColorExtensions.Parse(lv.GetProperty("cost").GetString()!),
				Reward = RewardJsonConverter.Parse(lv.GetProperty("reward"), new JsonSerializerOptions())
			});
		}

		return new CharacterCard
		{
			Id = el.GetProperty("id").GetInt32(),
			Name = el.GetProperty("name").GetString()!,
			Sectors = ParseSectors(el.GetProperty("sectors")),
			Levels = levels.OrderBy(l => l.Level).ToList(),
			Source = el.TryGetProperty("source", out var src) ? src.GetString() : null
		};
	}

	static LawCard ParseLaw(JsonElement el)
	{
		LawEffect? effect = null;
		if (el.TryGetProperty("effect", out var effectEl) && effectEl.ValueKind == JsonValueKind.Object)
		{
			effect = new LawEffect
			{
				Timing = effectEl.TryGetProperty("timing", out var t) ? t.GetString() : null,
				Summary = effectEl.TryGetProperty("summary", out var s) ? s.GetString() : null,
				Mechanizable = effectEl.TryGetProperty("mechanizable", out var m) && m.ValueKind == JsonValueKind.True
					? true
					: effectEl.TryGetProperty("mechanizable", out m) && m.ValueKind == JsonValueKind.False
						? false
						: null
			};
		}

		return new LawCard
		{
			Id = el.GetProperty("id").GetInt32(),
			Sectors = ParseSectors(el.GetProperty("sectors")),
			Text = el.GetProperty("text").GetString()!,
			Effect = effect,
			Source = el.TryGetProperty("source", out var src) ? src.GetString() : null
		};
	}

	static SectorColors ParseSectors(JsonElement el) => new()
	{
		Tl = GemColorExtensions.Parse(el.GetProperty("tl").GetString()!),
		Tr = GemColorExtensions.Parse(el.GetProperty("tr").GetString()!),
		Bl = GemColorExtensions.Parse(el.GetProperty("bl").GetString()!),
		Br = GemColorExtensions.Parse(el.GetProperty("br").GetString()!)
	};
}
