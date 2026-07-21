using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;

namespace Namestnik.Ai;

/// <summary>
/// Virtual opponent: sealed gem bid drawn from the box colors.
/// ~15% chance to bid a color with no cards (= attack: discard after humans resolve).
/// </summary>
public sealed class VirtualOpponent
{
	readonly Random _rng = new();

	public GameCommand ChooseAuctionAction(GameEngine engine, int playerId)
	{
		var state = engine.State;
		var legal = state.AuctionSlots
			.Where(s => s.AvailableCount > 0)
			.Select(s => s.Color)
			.Where(c => state.VirtualBox[c] > 0)
			.ToList();

		var emptyColors = Enum.GetValues<GemColor>()
			.Where(c => state.Slot(c).AvailableCount == 0 && state.VirtualBox[c] > 0)
			.ToList();

		// Solo rule: color with no cards counts as attack.
		if (emptyColors.Count > 0 && (_rng.NextDouble() < 0.15 || legal.Count == 0))
			return new BidAttackCommand(playerId);

		if (legal.Count == 0)
			return new PassAuctionCommand(playerId);

		return new BidGemCommand(playerId, legal[_rng.Next(legal.Count)]);
	}
}
