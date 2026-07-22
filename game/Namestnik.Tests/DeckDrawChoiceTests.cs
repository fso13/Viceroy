using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class DeckDrawChoiceTests
{
	[Fact]
	public void Card_reward_prompts_deck_choice_when_both_decks_available()
	{
		var engine = TestHelpers.NewSolo(seed: 11, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is CardReward);

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9101,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[def.GetLevel(1).Cost] = Math.Max(human.Screen[def.GetLevel(1).Cost], 2);

		engine.State.LawDeck.Clear();
		engine.State.LawDeck.Add(70);
		engine.State.SmallDeck.Clear();
		engine.State.SmallDeck.Add(db.Characters.Keys.First(id => id != def.Id));

		var handBefore = human.Hand.Count;
		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		Assert.NotNull(engine.State.PendingDeckDraw);
		Assert.Equal(0, engine.State.PendingDeckDraw!.PlayerId);
		Assert.Equal(1, engine.State.PendingDeckDraw.Remaining);
		Assert.Equal(DevelopmentSubPhase.ChoosingDeckDraw, engine.State.DevelopmentSubPhase);
		Assert.Equal(handBefore - 1, human.Hand.Count); // played card removed, not drawn yet

		TestHelpers.Apply(engine, new ChooseDeckDrawCommand(0, FromLawDeck: false));

		Assert.Null(engine.State.PendingDeckDraw);
		Assert.Contains(human.Hand, c => c.Kind == CardKind.Character && c.DefinitionId != def.Id);
		Assert.Empty(engine.State.SmallDeck);
		Assert.Single(engine.State.LawDeck);
	}

	[Fact]
	public void Card_reward_from_law_deck_when_chosen()
	{
		var engine = TestHelpers.NewSolo(seed: 12, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is CardReward { Amount: 1 });

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9102,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[def.GetLevel(1).Cost] = Math.Max(human.Screen[def.GetLevel(1).Cost], 2);

		engine.State.LawDeck.Clear();
		engine.State.LawDeck.Add(71);
		engine.State.SmallDeck.Clear();
		engine.State.SmallDeck.Add(db.Characters.Keys.First(id => id != def.Id));

		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));
		TestHelpers.Apply(engine, new ChooseDeckDrawCommand(0, FromLawDeck: true));

		Assert.Null(engine.State.PendingDeckDraw);
		Assert.Contains(human.Hand, c => c.Kind == CardKind.Law && c.DefinitionId == 71);
		Assert.Empty(engine.State.LawDeck);
	}

	[Fact]
	public void Card_reward_auto_draws_when_only_one_deck_has_cards()
	{
		var engine = TestHelpers.NewSolo(seed: 13, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is CardReward { Amount: 1 });

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9103,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[def.GetLevel(1).Cost] = Math.Max(human.Screen[def.GetLevel(1).Cost], 2);

		engine.State.LawDeck.Clear();
		engine.State.SmallDeck.Clear();
		var smallId = db.Characters.Keys.First(id => id != def.Id);
		engine.State.SmallDeck.Add(smallId);

		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		Assert.Null(engine.State.PendingDeckDraw);
		Assert.Contains(human.Hand, c => c.Kind == CardKind.Character && c.DefinitionId == smallId);
	}
}
