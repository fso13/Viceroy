using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class InvariantTests
{
	[Fact]
	public void Cards_load_64_characters_and_24_laws()
	{
		var db = TestHelpers.LoadCards();
		Assert.Equal(64, db.Characters.Count);
		Assert.Equal(24, db.Laws.Count);
	}

	[Fact]
	public void Setup_splits_big_48_and_small_remainder()
	{
		var engine = TestHelpers.NewSolo(seed: 7);
		// Big is filled to 48, then 4 are dealt to the auction row → 44 left in the deck.
		var onAuction = engine.State.AuctionSlots.Sum(s => s.AvailableCount);
		Assert.Equal(4, onAuction);
		Assert.Equal(48, engine.State.BigDeck.Count + onAuction);
		Assert.True(engine.State.SmallDeck.Count > 0);
	}

	[Fact]
	public void Gem_conservation_holds_at_setup()
	{
		var engine = TestHelpers.NewSolo(seed: 11);
		Assert.Equal(64, GemAccounting.TotalPhysical(engine.State));
		Assert.True(GemAccounting.Conserved(engine.State));
	}

	[Fact]
	public void Gem_conservation_after_pass_and_development_pass()
	{
		var engine = TestHelpers.NewSolo(seed: 3, virtuals: 1);
		// Seal virtual pass so resolve can proceed with human pass.
		var virt = engine.State.Players.First(p => p.Role == SessionRole.VirtualOpponent);
		TestHelpers.Apply(engine, new PassAuctionCommand(virt.PlayerId));
		TestHelpers.Apply(engine, new PassAuctionCommand(0));
		if (engine.State.AuctionSubPhase == AuctionSubPhase.ClaimingPassGems)
			TestHelpers.Apply(engine, new ClaimPassGemsCommand(0, Confirm: true));

		Assert.True(GemAccounting.Conserved(engine.State));

		if (engine.State.Phase == TurnPhase.Development)
		{
			TestHelpers.Apply(engine, new PassDevelopmentCommand(0));
			Assert.True(GemAccounting.Conserved(engine.State));
		}
	}

	[Fact]
	public void Clone_roundtrip_preserves_gem_total_and_decks()
	{
		var engine = TestHelpers.NewSolo(seed: 99);
		var clone = GameStateClone.Clone(engine.State);
		Assert.Equal(GemAccounting.TotalPhysical(engine.State), GemAccounting.TotalPhysical(clone));
		Assert.Equal(engine.State.BigDeck, clone.BigDeck);
		Assert.Equal(engine.State.LawDeck.Count, clone.LawDeck.Count);
		Assert.Equal(engine.State.Players[0].Hand.Count, clone.Players[0].Hand.Count);
	}

	[Fact]
	public void Undo_restores_hand_and_gems_after_discard()
	{
		var engine = TestHelpers.NewSolo(seed: 5, virtuals: 0);
		// Force development: pass auction alone.
		TestHelpers.Apply(engine, new PassAuctionCommand(0));
		if (engine.State.AuctionSubPhase == AuctionSubPhase.ClaimingPassGems)
			TestHelpers.Apply(engine, new ClaimPassGemsCommand(0, Confirm: true));

		Assert.Equal(TurnPhase.Development, engine.State.Phase);
		var human = TestHelpers.Human(engine);
		Assert.True(human.Hand.Count > 0);
		var handBefore = human.Hand.Count;
		var gemsBefore = human.Screen.TotalGems;

		engine.PushUndoCheckpoint();
		TestHelpers.Apply(engine, new DiscardForGemsCommand(0, 0));
		Assert.Equal(handBefore - 1, TestHelpers.Human(engine).Hand.Count);
		Assert.True(TestHelpers.Human(engine).Screen.TotalGems >= gemsBefore);

		Assert.True(engine.TryUndo(out _));
		Assert.Equal(handBefore, TestHelpers.Human(engine).Hand.Count);
		Assert.Equal(gemsBefore, TestHelpers.Human(engine).Screen.TotalGems);
		Assert.True(GemAccounting.Conserved(engine.State));
	}

	[Fact]
	public void Pyramid_max_height_invariant_helper()
	{
		var engine = TestHelpers.NewSolo(seed: 1);
		var p = TestHelpers.Human(engine);
		Assert.True(p.Pyramid.Height <= GameState.MaxPyramidLevels);
	}

	[Fact]
	public void Law66_does_not_duplicate_parked_gems()
	{
		var card = new PyramidCard
		{
			Card = new CardInstance { InstanceId = 1, Kind = CardKind.Character, DefinitionId = 1 },
			Level = 1,
			Index = 0,
			VictoryPoints = 2,
			ParkedGems = 3
		};
		Namestnik.Core.Laws.LawEffects.DuplicateTokens(card);
		Assert.Equal(4, card.VictoryPoints);
		Assert.Equal(3, card.ParkedGems);
	}

	[Fact]
	public void Attack_rejected_when_auction_empty_keeps_token()
	{
		var engine = TestHelpers.NewSolo(seed: 2, virtuals: 0);
		TestHelpers.Human(engine).Screen.AttackTokens = 1;
		foreach (var slot in engine.State.AuctionSlots)
		{
			slot.CardAtTip = null;
			slot.CardAtBase = null;
		}

		Assert.False(engine.TryApply(new BidAttackCommand(0), out var error));
		Assert.Contains("нет карт", error, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(1, TestHelpers.Human(engine).Screen.AttackTokens);
	}
}

