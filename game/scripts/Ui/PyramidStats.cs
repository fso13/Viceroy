using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Live pyramid totals for the status panel.</summary>
public static class PyramidStats
{
	public static string Format(PlayerState player)
	{
		var parts = new List<string>();

		var infiniteCards = player.Pyramid.AllCards.Where(c => c.InfiniteGem is not null).ToList();
		var available = infiniteCards
			.Where(c => !c.InfiniteUsedThisTurn)
			.GroupBy(c => c.InfiniteGem!.Value)
			.OrderBy(g => g.Key)
			.Select(g => $"{GemIcons.ColorName(g.Key)}×{g.Count()}")
			.ToList();
		var used = infiniteCards.Count(c => c.InfiniteUsedThisTurn);
		if (available.Count == 0 && infiniteCards.Count == 0)
			parts.Add("∞ камни: нет");
		else
		{
			var availText = available.Count > 0 ? string.Join(", ", available) : "нет свободных";
			parts.Add(used > 0
				? $"∞ камни: {availText} (исп. {used})"
				: $"∞ камни: {availText}");
		}

		var magic = player.MagicTokens;
		var bonusMag = player.Pyramid.AllCards.Sum(c => c.BonusMagic);
		var magicVp = magic > 0 && bonusMag > 0 ? magic * bonusMag : 0;
		parts.Add($"Магия: {magic}×{bonusMag}={magicVp} VP");

		var sci = player.ScienceTokens;
		var def = player.DefenseTokens;
		var vp = player.VictoryPointTokens;
		parts.Add($"Наука: {sci}");
		parts.Add($"Защита: {def}");
		parts.Add($"VP: {vp}");

		var sets = Math.Min(def, Math.Min(magic, sci));
		parts.Add($"Наборы: {sets}×12={sets * 12}");

		var parked = player.Pyramid.AllCards.Sum(c => c.ParkedGems);
		var tucked = player.Pyramid.AllCards.Sum(c => c.TuckedCards.Count);
		if (parked > 0)
			parts.Add($"Парковка: {parked}");
		if (tucked > 0)
			parts.Add($"Подложено: {tucked}");

		parts.Add($"Атака: {player.Screen.AttackTokens}");
		return string.Join("  ·  ", parts);
	}

	public static List<(string Text, Color Color)> TokenBadges(PyramidCard pc)
	{
		var list = new List<(string, Color)>();
		if (pc.VictoryPoints > 0)
			list.Add(($"VP{pc.VictoryPoints}", new Color(0.95f, 0.85f, 0.35f)));
		if (pc.Science > 0)
			list.Add(($"Н{pc.Science}", new Color(0.7f, 0.85f, 1f)));
		if (pc.Magic > 0)
			list.Add(($"М{pc.Magic}", new Color(0.85f, 0.55f, 1f)));
		if (pc.Defense > 0)
			list.Add(($"З{pc.Defense}", new Color(0.6f, 0.9f, 0.7f)));
		if (pc.BonusMagic > 0)
			list.Add(($"бМ+{pc.BonusMagic}", new Color(1f, 0.6f, 0.9f)));
		if (pc.InfiniteGem is GemColor inf)
			list.Add(($"∞{Short(inf)}", GemTint(inf)));
		if (pc.ParkedGems > 0)
			list.Add(($"◆{pc.ParkedGems}", new Color(0.6f, 0.75f, 1f)));
		if (pc.TuckedCards.Count > 0)
			list.Add(($"↓{pc.TuckedCards.Count}", new Color(0.85f, 0.75f, 0.55f)));
		foreach (var (color, amount) in pc.BonusCircles)
			list.Add(($"○{Short(color)}+{amount}", GemTint(color)));
		return list;
	}

	static string Short(GemColor c) => c switch
	{
		GemColor.Blue => "С",
		GemColor.Red => "К",
		GemColor.Green => "З",
		GemColor.Yellow => "Ж",
		_ => "?"
	};

	static Color GemTint(GemColor c) => c switch
	{
		GemColor.Blue => new Color(0.35f, 0.55f, 1f),
		GemColor.Red => new Color(1f, 0.35f, 0.3f),
		GemColor.Green => new Color(0.35f, 0.85f, 0.45f),
		GemColor.Yellow => new Color(1f, 0.85f, 0.25f),
		_ => Colors.White
	};
}
