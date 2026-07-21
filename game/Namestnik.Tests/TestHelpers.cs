using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;

namespace Namestnik.Tests;

public static class TestHelpers
{
	public static string CardsPath =>
		Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "cards.json"));

	public static CardDatabase LoadCards() => CardDatabase.LoadFromFile(CardsPath);

	public static GameEngine NewSolo(int seed = 42, int virtuals = 1) =>
		GameEngine.CreateNew(LoadCards(), GameMode.Solo, humanCount: 1, virtualCount: virtuals, seed: seed);

	public static void Apply(GameEngine engine, GameCommand cmd)
	{
		if (!engine.TryApply(cmd, out var error))
			throw new InvalidOperationException(error);
	}

	public static PlayerState Human(GameEngine engine) =>
		engine.State.Players.First(p => p.Role != SessionRole.VirtualOpponent);

	public static void PassAuctionUntilDevelopment(GameEngine engine, int playerId = 0)
	{
		var guard = 0;
		while (engine.State.Phase == TurnPhase.Auction && guard++ < 40)
		{
			var s = engine.State;
			if (s.AuctionSubPhase == AuctionSubPhase.ClaimingPassGems
			    && s.PendingPassGems?.PlayerId == playerId)
			{
				Apply(engine, new ClaimPassGemsCommand(playerId, Confirm: true));
				continue;
			}

			if (s.AuctionSubPhase == AuctionSubPhase.ChoosingCards && s.HasPendingChoice(playerId))
			{
				var opt = s.PendingCardChoices.First(c => c.PlayerId == playerId).Options[0];
				Apply(engine, new ChooseAuctionCardCommand(playerId, opt));
				continue;
			}

			if (s.AuctionSubPhase == AuctionSubPhase.CollectingBids
			    && !s.SealedBids.ContainsKey(playerId)
			    && !s.GetPlayer(playerId).HasPassedAuction
			    && !s.GetPlayer(playerId).AcquiredAuctionCardThisTurn)
			{
				Apply(engine, new PassAuctionCommand(playerId));
				continue;
			}

			// Wait for virtuals / resolution
			if (s.ActiveAuctionPlayers().Any(p => p.Role == SessionRole.VirtualOpponent))
			{
				foreach (var v in s.ActiveAuctionPlayers().Where(p => p.Role == SessionRole.VirtualOpponent))
				{
					if (!s.SealedBids.ContainsKey(v.PlayerId))
						Apply(engine, new PassAuctionCommand(v.PlayerId));
				}
			}
		}
	}
}
