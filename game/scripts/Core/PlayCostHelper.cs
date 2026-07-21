using Namestnik.Core.Models;

namespace Namestnik.Core;

/// <summary>Character play costs with optional infinite-gem discounts.</summary>
public static class PlayCostHelper
{
	public static Dictionary<GemColor, int> BaseCost(CharacterCard def, int level) =>
		def.CostToPlayAt(level);

	/// <summary>
	/// Cost after applying unused infinite gems on the player's pyramid (each ∞ pays 1 of its color).
	/// </summary>
	public static Dictionary<GemColor, int> EffectiveCost(
		PlayerState player,
		CharacterCard def,
		int level,
		bool useInfinites = true)
	{
		var cost = BaseCost(def, level);
		if (!useInfinites)
			return cost;

		var clone = cost.ToDictionary(kv => kv.Key, kv => kv.Value);
		ApplyInfinites(player, clone, markUsed: false);
		return clone;
	}

	public static bool CanAfford(
		PlayerState player,
		CharacterCard def,
		int level,
		bool useInfinites = true) =>
		player.Screen.CanAfford(EffectiveCost(player, def, level, useInfinites));

	/// <param name="markUsed">If true, sets InfiniteUsedThisTurn on spent infinites.</param>
	public static void ApplyInfinites(PlayerState player, Dictionary<GemColor, int> cost, bool markUsed)
	{
		foreach (var card in player.Pyramid.AllCards)
		{
			if (card.InfiniteGem is not GemColor color)
				continue;
			if (card.InfiniteUsedThisTurn)
				continue;
			if (cost.GetValueOrDefault(color) <= 0)
				continue;

			cost[color]--;
			if (markUsed)
				card.InfiniteUsedThisTurn = true;
		}
	}
}
