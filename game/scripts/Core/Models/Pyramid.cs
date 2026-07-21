namespace Namestnik.Core.Models;

/// <summary>
/// Power pyramid. Level 1 is the base row. Level N&gt;1 cards sit on two adjacent cards of level N-1
/// (adjacent = consecutive entries in that row's list).
/// </summary>
public sealed class Pyramid
{
	public Dictionary<int, List<PyramidCard>> Rows { get; } = new();

	public IEnumerable<PyramidCard> AllCards =>
		Rows.Values.SelectMany(r => r);

	public int Height => Rows.Count == 0 ? 0 : Rows.Keys.Max();

	public int BaseCount => Rows.TryGetValue(1, out var row) ? row.Count : 0;

	public void PlaceStarter(PyramidCard card)
	{
		card.Level = 1;
		card.Index = 0;
		Rows[1] = [card];
	}

	/// <param name="index">
	/// L1: 0 = append left, Count = append right.
	/// L2+: array index of the left support card on the row below.
	/// </param>
	public void Place(PyramidCard card)
	{
		if (card.Level == 1)
			PlaceOnBase(card, onLeft: card.Index == 0);
		else
			PlaceAbove(card);
	}

	void PlaceOnBase(PyramidCard card, bool onLeft)
	{
		if (!Rows.TryGetValue(1, out var row))
		{
			Rows[1] = [card];
			card.Index = 0;
			return;
		}

		if (onLeft)
		{
			row.Insert(0, card);
			foreach (var lvl in Rows.Keys.Where(l => l > 1).ToList())
			{
				foreach (var c in Rows[lvl])
					c.Index++;
			}
		}
		else
		{
			row.Add(card);
		}

		Renumber(1);
	}

	void PlaceAbove(PyramidCard card)
	{
		if (!Rows.TryGetValue(card.Level - 1, out var below) || below.Count < 2)
			throw new InvalidOperationException("Недостаточно карт на нижнем уровне.");
		if (card.Index < 0 || card.Index + 1 >= below.Count)
			throw new InvalidOperationException("Некорректная опора.");
		if (!IsSupportFree(card.Level - 1, card.Index))
			throw new InvalidOperationException("Место уже занято.");

		if (!Rows.TryGetValue(card.Level, out var row))
		{
			row = [];
			Rows[card.Level] = row;
		}

		row.Add(card);
		row.Sort((a, b) => a.Index.CompareTo(b.Index));
	}

	void Renumber(int level)
	{
		var row = Rows[level];
		for (var i = 0; i < row.Count; i++)
			row[i].Index = i;
	}

	public bool IsSupportFree(int belowLevel, int leftIndex)
	{
		if (!Rows.TryGetValue(belowLevel + 1, out var above))
			return true;
		return above.All(c => c.Index != leftIndex);
	}

	public List<(int Level, int Index)> LegalPlacements(int maxLevel = GameState.MaxPyramidLevels)
	{
		var result = new List<(int, int)>();
		if (BaseCount == 0)
		{
			result.Add((1, 0));
			return result;
		}

		result.Add((1, 0)); // left
		result.Add((1, BaseCount)); // right

		for (var level = 2; level <= maxLevel; level++)
		{
			if (!Rows.TryGetValue(level - 1, out var below) || below.Count < 2)
				continue;
			for (var i = 0; i < below.Count - 1; i++)
			{
				if (IsSupportFree(level - 1, i))
					result.Add((level, i));
			}
		}

		return result;
	}

	public (PyramidCard Left, PyramidCard Right)? SupportsOf(PyramidCard upper)
	{
		if (upper.Level < 2)
			return null;
		if (!Rows.TryGetValue(upper.Level - 1, out var below))
			return null;
		if (upper.Index < 0 || upper.Index + 1 >= below.Count)
			return null;
		return (below[upper.Index], below[upper.Index + 1]);
	}

	/// <summary>All upper cards that form a circle (level ≥ 2).</summary>
	public IEnumerable<PyramidCard> CircleUppers() =>
		AllCards.Where(c => c.Level >= 2 && SupportsOf(c) is not null);

	public PyramidCard? FindByInstanceId(int instanceId) =>
		AllCards.FirstOrDefault(c => c.Card.InstanceId == instanceId);

	public bool IsFree(PyramidCard card)
	{
		if (!Rows.TryGetValue(card.Level, out var row))
			return false;
		var pos = row.IndexOf(card);
		if (pos < 0)
			return false;
		if (pos != 0 && pos != row.Count - 1)
			return false;
		if (card.TuckedCards.Count > 0)
			return false;
		if (Rows.TryGetValue(card.Level + 1, out var above))
		{
			foreach (var up in above)
			{
				var s = SupportsOf(up);
				if (s is { } ss && (ReferenceEquals(ss.Left, card) || ReferenceEquals(ss.Right, card)))
					return false;
			}
		}

		return true;
	}

	public IEnumerable<PyramidCard> FreeCards() => AllCards.Where(IsFree);

	/// <summary>Replace card contents in-place (law 65).</summary>
	public void ReplaceCard(PyramidCard slot, CardInstance newCard)
	{
		// Keep level/index; caller transfers tokens onto slot, then assigns Card.
		slot.Card = newCard;
	}

	public void RemoveFreeCard(PyramidCard card)
	{
		if (!IsFree(card))
			throw new InvalidOperationException("Карта не свободная.");
		if (!Rows.TryGetValue(card.Level, out var row))
			throw new InvalidOperationException("Карта не в пирамиде.");
		var pos = row.IndexOf(card);
		row.RemoveAt(pos);
		if (row.Count == 0)
			Rows.Remove(card.Level);

		if (card.Level == 1)
		{
			foreach (var lvl in Rows.Keys.Where(l => l > 1).ToList())
			{
				foreach (var c in Rows[lvl])
				{
					if (c.Index > pos)
						c.Index--;
				}
			}

			if (Rows.ContainsKey(1))
				Renumber(1);
		}
	}

	public List<PyramidCard> NeighborsOf(PyramidCard card)
	{
		var result = new List<PyramidCard>();
		if (!Rows.TryGetValue(card.Level, out var row))
			return result;

		var pos = row.IndexOf(card);
		if (pos < 0)
			return result;

		if (pos > 0)
			result.Add(row[pos - 1]);
		if (pos + 1 < row.Count)
			result.Add(row[pos + 1]);

		var supports = SupportsOf(card);
		if (supports is { } s)
		{
			result.Add(s.Left);
			result.Add(s.Right);
		}

		if (Rows.TryGetValue(card.Level + 1, out var above))
		{
			foreach (var up in above)
			{
				var upSupports = SupportsOf(up);
				if (upSupports is { } us && (us.Left == card || us.Right == card))
					result.Add(up);
			}
		}

		return result.Distinct().ToList();
	}
}

public sealed class PyramidCard
{
	public required CardInstance Card { get; set; }
	public required int Level { get; set; }
	public required int Index { get; set; }

	public int VictoryPoints { get; set; }
	public int Science { get; set; }
	public int Magic { get; set; }
	public int Defense { get; set; }
	public GemColor? InfiniteGem { get; set; }
	public bool InfiniteUsedThisTurn { get; set; }
	public int BonusMagic { get; set; }
	public List<(GemColor Color, int Amount)> BonusCircles { get; } = new();

	/// <summary>Cards tucked under this one (laws 66/72/74/78).</summary>
	public List<CardInstance> TuckedCards { get; } = new();

	/// <summary>Gems parked on this card (law 76).</summary>
	public int ParkedGems { get; set; }

	/// <summary>Sector recolor overrides for final scoring (tl/tr/bl/br).</summary>
	public Dictionary<string, GemColor> SectorOverrides { get; } = new();

	public GemColor EffectiveSector(SectorColors printed, string corner)
	{
		if (SectorOverrides.TryGetValue(corner, out var over))
			return over;
		return printed.Get(corner);
	}
}

public enum DevelopmentSubPhase
{
	CollectingActions,
	ChoosingLevel5Reward,
	ChoosingReward,
	ResolvingLaw,
	Done
}

public abstract record DevelopmentAction;

/// <param name="LawTargetInstanceId">Target pyramid card for laws 65/66/74.</param>
/// <param name="ExtraHandIndices">Extra hand cards to tuck (law 72), excluding the played law.</param>
public sealed record PlayDevelopmentAction(
	int HandIndex,
	int Level,
	int Index,
	bool UseInfinites = true,
	int? LawTargetInstanceId = null,
	IReadOnlyList<int>? ExtraHandIndices = null)
	: DevelopmentAction;

public sealed record DiscardDevelopmentAction(int HandIndex) : DevelopmentAction;

public sealed record PassDevelopmentAction : DevelopmentAction;

public sealed class PendingLevel5Choice
{
	public required int PlayerId { get; init; }
	public required int PyramidCardInstanceId { get; init; }
	public required int CharacterDefinitionId { get; init; }
}
