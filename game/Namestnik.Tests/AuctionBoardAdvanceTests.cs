using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class AuctionBoardAdvanceTests
{
	[Fact]
	public void End_of_auction_discards_tip_slides_base_down_deals_new_base()
	{
		var engine = TestHelpers.NewSolo(seed: 7, virtuals: 0);
		var tipsBefore = engine.State.AuctionSlots.Select(s => s.CardAtTip).ToList();
		var basesBefore = engine.State.AuctionSlots.Select(s => s.CardAtBase).ToList();
		Assert.All(tipsBefore, t => Assert.Null(t));
		Assert.All(basesBefore, b => Assert.NotNull(b));

		// Put a tip card on each slot to simulate mid-game board.
		for (var i = 0; i < 4; i++)
		{
			var tipId = engine.State.BigDeck[0];
			engine.State.BigDeck.RemoveAt(0);
			engine.State.AuctionSlots[i].CardAtTip = tipId;
		}

		var tips = engine.State.AuctionSlots.Select(s => s.CardAtTip!.Value).ToList();
		var bases = engine.State.AuctionSlots.Select(s => s.CardAtBase!.Value).ToList();
		var discardBefore = engine.State.Discard.Count;
		var deckBefore = engine.State.BigDeck.Count;

		TestHelpers.PassAuctionUntilDevelopment(engine);

		Assert.Equal(TurnPhase.Development, engine.State.Phase);
		Assert.Equal(discardBefore + 4, engine.State.Discard.Count);
		foreach (var tip in tips)
			Assert.Contains(tip, engine.State.Discard);

		for (var i = 0; i < 4; i++)
		{
			Assert.Equal(bases[i], engine.State.AuctionSlots[i].CardAtTip);
			Assert.NotNull(engine.State.AuctionSlots[i].CardAtBase);
			Assert.NotEqual(bases[i], engine.State.AuctionSlots[i].CardAtBase);
		}

		Assert.Equal(deckBefore - 4, engine.State.BigDeck.Count);
	}
}
