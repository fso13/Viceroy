using Namestnik.Core.Models;

namespace Namestnik.Core.Scoring;

public sealed class FinalScorer
{
	readonly CardDatabase _db;

	public FinalScorer(CardDatabase db) => _db = db;

	public MatchResult ScoreMatch(GameState state)
	{
		foreach (var player in state.Players)
		{
			foreach (var card in player.Hand)
				state.Discard.Add(card.DefinitionId);
			player.Hand.Clear();
		}

		foreach (var player in state.Players.Where(p => p.Role != SessionRole.VirtualOpponent))
			GreedyRecolor(player);

		var scores = state.Players
			.Where(p => p.Role != SessionRole.VirtualOpponent)
			.Select(p => ScorePlayer(state, p))
			.ToList();

		if (scores.Count == 0)
			scores = state.Players.Select(p => ScorePlayer(state, p)).ToList();

		var best = scores.Max(s => s.Total);
		var winners = scores.Where(s => s.Total == best).Select(s => s.PlayerId).ToList();
		return new MatchResult { Scores = scores, WinnerIds = winners };
	}

	ScoreBreakdown ScorePlayer(GameState state, PlayerState player)
	{
		var bd = new ScoreBreakdown
		{
			PlayerId = player.PlayerId,
			DisplayName = player.DisplayName
		};

		var circleBonus = SumCircleBonuses(player);
		var circleCount = 0;
		foreach (var upper in player.Pyramid.CircleUppers())
		{
			if (!TryGetCircleColor(player, upper, out var color))
				continue;
			circleCount++;
			bd.Circles += upper.Level + circleBonus[color];
		}

		var infiniteCount = 0;
		foreach (var card in player.Pyramid.AllCards)
		{
			if (card.InfiniteGem is not GemColor color)
				continue;
			infiniteCount++;
			bd.Infinites += card.Level + circleBonus[color];
		}

		bd.Notes.Add($"Одноцветных кругов: {circleCount}, неисчерпаемых: {infiniteCount}");
		bd.VpTokens = player.VictoryPointTokens;

		var magicCount = player.MagicTokens;
		var magicBonus = player.Pyramid.AllCards.Sum(c => c.BonusMagic);
		bd.Magic = magicCount > 0 && magicBonus > 0 ? magicCount * magicBonus : 0;

		var setCount = Math.Min(player.DefenseTokens, Math.Min(player.MagicTokens, player.ScienceTokens));
		bd.Sets = 12 * setCount;

		var enemyAttacks = state.Players
			.Where(p => p.PlayerId != player.PlayerId)
			.Sum(p => p.Screen.AttackTokens);
		bd.AttackPenalty = 4 * Math.Max(0, enemyAttacks - player.DefenseTokens);

		bd.Laws = ScoreLaws(player, circleCount, infiniteCount, setCount, bd);
		return bd;
	}

	static Dictionary<GemColor, int> SumCircleBonuses(PlayerState player)
	{
		var map = new Dictionary<GemColor, int>
		{
			[GemColor.Blue] = 0,
			[GemColor.Red] = 0,
			[GemColor.Green] = 0,
			[GemColor.Yellow] = 0
		};
		foreach (var card in player.Pyramid.AllCards)
		{
			foreach (var (color, amount) in card.BonusCircles)
				map[color] += amount;
		}

		return map;
	}

	bool TryGetCircleColor(PlayerState player, PyramidCard upper, out GemColor color)
	{
		color = default;
		var supports = player.Pyramid.SupportsOf(upper);
		if (supports is null)
			return false;
		var (left, right) = supports.Value;
		GemColor[] sectors =
		[
			Effective(left, "tr"),
			Effective(right, "tl"),
			Effective(upper, "bl"),
			Effective(upper, "br")
		];
		if (sectors.Distinct().Count() != 1)
			return false;
		color = sectors[0];
		return true;
	}

	GemColor Effective(PyramidCard card, string corner)
	{
		var printed = card.Card.Kind == CardKind.Character
			? _db.GetCharacter(card.Card.DefinitionId).Sectors
			: _db.GetLaw(card.Card.DefinitionId).Sectors;
		return card.EffectiveSector(printed, corner);
	}

	int ScoreLaws(PlayerState player, int circleCount, int infiniteCount, int setCount, ScoreBreakdown bd)
	{
		var total = 0;
		foreach (var card in player.Pyramid.AllCards.Where(c => c.Card.Kind == CardKind.Law))
		{
			var id = card.Card.DefinitionId;
			var pts = id switch
			{
				68 => player.Pyramid.AllCards.Sum(c => 1 + c.TuckedCards.Count),
				72 => 2 * card.TuckedCards.Count,
				74 => card.Level switch { 1 => 3, 2 => 6, 3 => 9, 4 => 12, _ => 0 },
				75 => 3 * setCount,
				76 => 2 * card.ParkedGems,
				78 => 4 * card.TuckedCards.Count,
				79 => circleCount + infiniteCount,
				85 or 86 or 87 or 88 => 2 * player.Pyramid.NeighborsOf(card)
					.Count(n => n.Card.Kind == CardKind.Character),
				_ => 0
			};
			if (pts == 0)
				continue;
			total += pts;
			bd.Notes.Add($"Закон #{id}: +{pts}");
		}

		return total;
	}

	void GreedyRecolor(PlayerState player)
	{
		var changed = true;
		while (changed)
		{
			changed = false;
			foreach (var upper in player.Pyramid.CircleUppers().ToList())
			{
				if (TryGetCircleColor(player, upper, out _))
					continue;

				var supports = player.Pyramid.SupportsOf(upper);
				if (supports is null)
					continue;
				var (left, right) = supports.Value;
				var corners = new (PyramidCard Card, string Corner)[]
				{
					(left, "tr"),
					(right, "tl"),
					(upper, "bl"),
					(upper, "br")
				};

				foreach (GemColor target in Enum.GetValues<GemColor>())
				{
					var mismatches = corners
						.Where(c => Effective(c.Card, c.Corner) != target)
						.ToList();
					if (mismatches.Count is 0 or > 2)
						continue;
					if (player.Screen[target] < mismatches.Count)
						continue;

					foreach (var m in mismatches)
					{
						player.Screen.TrySpend(target);
						m.Card.SectorOverrides[m.Corner] = target;
					}

					changed = true;
					break;
				}

				if (changed)
					break;
			}
		}
	}
}
