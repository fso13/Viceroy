namespace Namestnik.Core.Models;

public sealed class SectorColors
{
	public required GemColor Tl { get; init; }
	public required GemColor Tr { get; init; }
	public required GemColor Bl { get; init; }
	public required GemColor Br { get; init; }

	public GemColor Get(string corner) => corner.ToLowerInvariant() switch
	{
		"tl" => Tl,
		"tr" => Tr,
		"bl" => Bl,
		"br" => Br,
		_ => throw new ArgumentException(corner)
	};
}

public sealed class LevelReward
{
	public required int Level { get; init; }
	public required GemColor Cost { get; init; }
	public required Reward Reward { get; init; }
}

public sealed class CharacterCard
{
	public required int Id { get; init; }
	public required string Name { get; init; }
	public required SectorColors Sectors { get; init; }
	public required IReadOnlyList<LevelReward> Levels { get; init; }
	public string? Source { get; init; }

	public LevelReward GetLevel(int level) =>
		Levels.First(l => l.Level == level);

	/// <summary>Gem costs to play at the given pyramid level (1..5).</summary>
	public Dictionary<GemColor, int> CostToPlayAt(int level)
	{
		if (level is < 1 or > 5)
			throw new ArgumentOutOfRangeException(nameof(level));

		var cost = new Dictionary<GemColor, int>
		{
			[GemColor.Blue] = 0,
			[GemColor.Red] = 0,
			[GemColor.Green] = 0,
			[GemColor.Yellow] = 0
		};

		var upTo = Math.Min(level, 4);
		for (var l = 1; l <= upTo; l++)
		{
			var c = GetLevel(l).Cost;
			cost[c]++;
		}

		if (level == 5)
		{
			var top = GetLevel(4).Cost;
			cost[top]++;
		}

		return cost;
	}
}

public sealed class LawEffect
{
	public string? Timing { get; init; }
	public string? Summary { get; init; }
	public bool? Mechanizable { get; init; }
}

public sealed class LawCard
{
	public required int Id { get; init; }
	public required SectorColors Sectors { get; init; }
	public required string Text { get; init; }
	public LawEffect? Effect { get; init; }
	public string? Source { get; init; }
}

public enum CardKind
{
	Character,
	Law
}

/// <summary>Runtime instance of a card in hand / auction.</summary>
public sealed class CardInstance
{
	public required int InstanceId { get; init; }
	public required CardKind Kind { get; init; }
	public required int DefinitionId { get; init; }

	public int OrderId => DefinitionId;
}
