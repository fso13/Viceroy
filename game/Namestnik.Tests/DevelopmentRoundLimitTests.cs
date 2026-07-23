using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class DevelopmentRoundLimitTests
{
	[Fact]
	public void Second_play_in_same_round_is_rejected()
	{
		var engine = TestHelpers.NewSolo(seed: 11, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		Assert.Equal(TurnPhase.Development, engine.State.Phase);
		Assert.Equal(1, engine.State.DevelopmentRound);

		// Force a cheap L1 play: clear pyramid to starter-only and give two affordable cards.
		human.Hand.Clear();
		human.Hand.Add(new CardInstance { InstanceId = 100, Kind = CardKind.Law, DefinitionId = 68 });
		human.Hand.Add(new CardInstance { InstanceId = 101, Kind = CardKind.Law, DefinitionId = 75 });
		foreach (GemColor c in Enum.GetValues<GemColor>())
			human.Screen[c] = 5;

		var place = human.Pyramid.LegalPlacements().First(p => p.Level == 1);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, place.Level, place.Index));

		// After resolve, either phase ended or next round started — never two plays at round 1.
		if (engine.State.Phase == TurnPhase.Development)
		{
			Assert.True(engine.State.DevelopmentRound >= 2);
			Assert.False(human.ActedThisDevelopmentRound);
		}
	}

	[Fact]
	public void At_most_three_plays_per_development_phase()
	{
		var engine = TestHelpers.NewSolo(seed: 13, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);

		var plays = 0;
		var guard = 0;
		while (engine.State.Phase == TurnPhase.Development && guard++ < 20)
		{
			// Resolve pending prompts if any.
			if (engine.State.PendingLevel5 is not null)
			{
				TestHelpers.Apply(engine, new ChooseLevel5RewardCommand(0, TakeFifteenVp: true));
				continue;
			}

			if (engine.State.PendingRewardChoice is { } rc)
			{
				TestHelpers.Apply(engine, new ChooseRewardCommand(0, 0));
				continue;
			}

			if (engine.State.PendingDeckDraw is not null)
			{
				TestHelpers.Apply(engine, new ChooseDeckDrawCommand(0, FromLawDeck: true));
				continue;
			}

			if (engine.State.PendingLaw is not null)
			{
				TestHelpers.Apply(engine, new ResolveLawCommand(0, OptionIndex: 0));
				continue;
			}

			if (engine.State.DevelopmentSubPhase != DevelopmentSubPhase.CollectingActions)
				break;

			if (human.HasPassedDevelopment || human.ActedThisDevelopmentRound)
				break;

			// Prefer playing a law cheaply onto L1 if possible; else pass.
			human.Hand.Clear();
			human.Hand.Add(new CardInstance
			{
				InstanceId = 200 + plays,
				Kind = CardKind.Law,
				DefinitionId = 68
			});
			var legal = human.Pyramid.LegalPlacements().Where(p => p.Level < 5).ToList();
			if (legal.Count == 0)
			{
				TestHelpers.Apply(engine, new PassDevelopmentCommand(0));
				break;
			}

			var spot = legal[0];
			var roundBefore = engine.State.DevelopmentRound;
			TestHelpers.Apply(engine, new PlayCardCommand(0, 0, spot.Level, spot.Index));
			plays++;
			Assert.True(plays <= 3, $"Played {plays} cards in one development phase");
			if (engine.State.Phase == TurnPhase.Development)
				Assert.True(engine.State.DevelopmentRound > roundBefore || human.HasPassedDevelopment
					|| engine.State.DevelopmentSubPhase != DevelopmentSubPhase.CollectingActions);
		}

		Assert.InRange(plays, 1, 3);
	}
}
