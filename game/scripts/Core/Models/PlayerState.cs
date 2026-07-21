namespace Namestnik.Core.Models;

public sealed class GemWallet
{
	readonly Dictionary<GemColor, int> _gems = new()
	{
		[GemColor.Blue] = 0,
		[GemColor.Red] = 0,
		[GemColor.Green] = 0,
		[GemColor.Yellow] = 0
	};

	public int AttackTokens { get; set; }

	public int this[GemColor color]
	{
		get => _gems[color];
		set => _gems[color] = Math.Max(0, value);
	}

	public int TotalGems => _gems.Values.Sum();

	public bool CanPay(GemColor color, int amount = 1) => _gems[color] >= amount;

	public void Add(GemColor color, int amount = 1) => _gems[color] += amount;

	public bool TrySpend(GemColor color, int amount = 1)
	{
		if (!CanPay(color, amount))
			return false;
		_gems[color] -= amount;
		return true;
	}

	public bool CanAfford(Dictionary<GemColor, int> cost)
	{
		foreach (var (color, amount) in cost)
		{
			if (amount > 0 && !CanPay(color, amount))
				return false;
		}

		return true;
	}

	public void Spend(Dictionary<GemColor, int> cost)
	{
		foreach (var (color, amount) in cost)
		{
			if (amount <= 0)
				continue;
			if (!TrySpend(color, amount))
				throw new InvalidOperationException($"Не хватает камней {color}.");
		}
	}

	public Dictionary<GemColor, int> Snapshot() => new(_gems);
}

public sealed class PlayerState
{
	public required int PlayerId { get; init; }
	public required string DisplayName { get; init; }
	public required SessionRole Role { get; init; }

	public GemWallet Screen { get; } = new();
	public List<CardInstance> Hand { get; } = new();
	public Pyramid Pyramid { get; } = new();

	public bool HasPassedAuction { get; set; }
	public bool HasPassedDevelopment { get; set; }
	public bool AcquiredAuctionCardThisTurn { get; set; }
	public bool ActedThisDevelopmentRound { get; set; }

	/// <summary>Law 69: skip next auction and take a face-up card instead.</summary>
	public bool SkipNextAuction { get; set; }

	public bool HasTuckOnDrawLaw =>
		Pyramid.AllCards.Any(c => c.Card.Kind == CardKind.Law && c.Card.DefinitionId == LawIds.TuckOnDraw);

	public int ScienceTokens => Pyramid.AllCards.Sum(c => c.Science);
	public int MagicTokens => Pyramid.AllCards.Sum(c => c.Magic);
	public int DefenseTokens => Pyramid.AllCards.Sum(c => c.Defense);
	public int VictoryPointTokens => Pyramid.AllCards.Sum(c => c.VictoryPoints);

	public int PriorityKey => LowestOrderInPyramid() ?? int.MaxValue;

	public int? LowestOrderInPyramid()
	{
		var cards = Pyramid.AllCards.ToList();
		if (cards.Count == 0)
			return null;
		return cards.Min(c => c.Card.OrderId);
	}

	public void ResetInfiniteUses()
	{
		foreach (var card in Pyramid.AllCards)
			card.InfiniteUsedThisTurn = false;
	}
}
