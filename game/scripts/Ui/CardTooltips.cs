using System.Text;
using Namestnik.Core;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

public static class CardTooltips
{
	public static string ForCharacter(CharacterCard def)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"{def.Name} #{def.Id}");
		sb.AppendLine(
			$"Секторы: ⌜{ColorRu(def.Sectors.Tl)} {ColorRu(def.Sectors.Tr)}⌝ / ⌞{ColorRu(def.Sectors.Bl)} {ColorRu(def.Sectors.Br)}⌟");
		foreach (var level in def.Levels.OrderBy(l => l.Level))
		{
			var cost = string.Join("+",
				Enumerable.Range(1, level.Level).Select(l => ColorRu(def.GetLevel(l).Cost)));
			sb.AppendLine($"Ур.{level.Level} (оплата: {cost}): {DescribeReward(level.Reward)}");
		}

		sb.Append("Ур.5: награды 1–3 или 15 VP");
		return sb.ToString();
	}

	public static string ForLaw(LawCard law)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Закон #{law.Id}");
		sb.AppendLine(
			$"Секторы: ⌜{ColorRu(law.Sectors.Tl)} {ColorRu(law.Sectors.Tr)}⌝ / ⌞{ColorRu(law.Sectors.Bl)} {ColorRu(law.Sectors.Br)}⌟");
		if (law.Effect?.Summary is { } summary)
			sb.AppendLine(summary);
		sb.Append(law.Text);
		return sb.ToString();
	}

	public static string ForCard(CardDatabase db, CardKind kind, int definitionId) =>
		kind == CardKind.Character && db.Characters.TryGetValue(definitionId, out var ch)
			? ForCharacter(ch)
			: kind == CardKind.Law && db.Laws.TryGetValue(definitionId, out var law)
				? ForLaw(law)
				: $"#{definitionId}";

	public static string ForPyramidCard(CardDatabase db, PyramidCard pc)
	{
		var head = ForCard(db, pc.Card.Kind, pc.Card.DefinitionId);
		var tokens = new List<string>();
		if (pc.VictoryPoints > 0) tokens.Add($"VP:{pc.VictoryPoints}");
		if (pc.Science > 0) tokens.Add($"Наука:{pc.Science}");
		if (pc.Magic > 0) tokens.Add($"Магия:{pc.Magic}");
		if (pc.Defense > 0) tokens.Add($"Защита:{pc.Defense}");
		if (pc.BonusMagic > 0) tokens.Add($"Бонус маг:{pc.BonusMagic}");
		if (pc.InfiniteGem is GemColor inf) tokens.Add($"∞ {ColorRu(inf)}");
		if (pc.ParkedGems > 0) tokens.Add($"Камни:{pc.ParkedGems}");
		if (pc.TuckedCards.Count > 0) tokens.Add($"Подложено:{pc.TuckedCards.Count}");
		if (tokens.Count == 0)
			return $"{head}\nУровень {pc.Level}";
		return $"{head}\nУровень {pc.Level}\nНа карте: {string.Join(", ", tokens)}";
	}

	public static string DescribeReward(Reward reward) => reward switch
	{
		VpReward vp => $"+{vp.Amount} VP",
		GemsReward g => $"+{g.Amount} камней",
		CardReward c => $"+{c.Amount} карт",
		ScienceReward s => $"+{s.Amount} наука",
		MagicReward m => $"+{m.Amount} магия",
		DefenseReward d => $"+{d.Amount} защита",
		AttackReward a => $"+{a.Amount} атака",
		InfiniteReward i => $"∞ {ColorRu(i.Color)}",
		BonusMagicReward b => $"бонус магии +{b.Amount}",
		BonusCircleReward bc => $"бонус круга {ColorRu(bc.Color)} +{bc.Amount}",
		MultiReward multi => string.Join(" + ", multi.Parts.Select(DescribeReward)),
		ChoiceReward choice => "выбор: " + string.Join(" / ", choice.Options.Select(DescribeReward)),
		_ => reward.GetType().Name
	};

	public static string ColorRu(GemColor c) => c switch
	{
		GemColor.Blue => "син",
		GemColor.Red => "крас",
		GemColor.Green => "зел",
		GemColor.Yellow => "жёл",
		_ => c.ToString()
	};
}
