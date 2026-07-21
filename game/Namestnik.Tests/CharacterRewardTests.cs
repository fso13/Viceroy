using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class CharacterRewardTests
{
	[Fact]
	public void Playing_character_with_science_puts_token_on_card()
	{
		var engine = TestHelpers.NewSolo(seed: 8, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is ScienceReward);
		var sci = (ScienceReward)def.GetLevel(1).Reward;

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9002,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[def.GetLevel(1).Cost] = Math.Max(human.Screen[def.GetLevel(1).Cost], 2);

		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		var placed = human.Pyramid.AllCards.First(c => c.Card.InstanceId == 9002);
		Assert.Equal(sci.Amount, placed.Science);
	}

	[Fact]
	public void Playing_character_with_gems_adds_gems_behind_screen()
	{
		var engine = TestHelpers.NewSolo(seed: 21, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is GemsReward);
		var gemsReward = (GemsReward)def.GetLevel(1).Reward;
		var costColor = def.GetLevel(1).Cost;

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9001,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[costColor] = Math.Max(human.Screen[costColor], 2);

		// Ensure reserve can pay the gem reward.
		foreach (GemColor c in Enum.GetValues<GemColor>())
			engine.State.Reserve[c] = Math.Max(engine.State.Reserve[c], gemsReward.Amount);

		var gemsBefore = human.Screen.TotalGems;
		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		Assert.True(human.Pyramid.AllCards.Any(c => c.Card.InstanceId == 9001));
		Assert.Equal(gemsBefore - 1 + gemsReward.Amount, human.Screen.TotalGems);
	}

	[Fact]
	public void Playing_character_with_vp_puts_points_on_card()
	{
		var engine = TestHelpers.NewSolo(seed: 3, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		var def = db.Characters.Values.First(c =>
			c.GetLevel(1).Reward is VpReward);
		var vp = (VpReward)def.GetLevel(1).Reward;

		human.Hand.Clear();
		human.Hand.Add(new CardInstance
		{
			InstanceId = 9003,
			Kind = CardKind.Character,
			DefinitionId = def.Id
		});
		human.Screen[def.GetLevel(1).Cost] = Math.Max(human.Screen[def.GetLevel(1).Cost], 2);

		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		var placed = human.Pyramid.AllCards.First(c => c.Card.InstanceId == 9003);
		Assert.Equal(vp.Amount, placed.VictoryPoints);
	}
}
