using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Namestnik.Net;
using Xunit;

namespace Namestnik.Tests;

public class RecolorPhaseTests
{
	[Fact]
	public void Recolor_paints_sector_spends_gem_and_affects_circle_score()
	{
		var engine = TestHelpers.NewSolo(seed: 7, virtuals: 0);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		human.Pyramid.Rows.Clear();
		human.Hand.Clear();

		// L1: green tr + green tl supports; L2 upper with mismatched bl/br that we will paint green.
		human.Pyramid.PlaceStarter(new PyramidCard
		{
			Card = new CardInstance { InstanceId = 1, Kind = CardKind.Character, DefinitionId = 39 },
			Level = 1,
			Index = 0
		});
		human.Pyramid.Place(new PyramidCard
		{
			Card = new CardInstance { InstanceId = 2, Kind = CardKind.Character, DefinitionId = 51 },
			Level = 1,
			Index = 1
		});
		var upper = new PyramidCard
		{
			Card = new CardInstance { InstanceId = 3, Kind = CardKind.Character, DefinitionId = 1 },
			Level = 2,
			Index = 0
		};
		human.Pyramid.Place(upper);

		foreach (GemColor c in Enum.GetValues<GemColor>())
			human.Screen[c] = 0;
		human.Screen[GemColor.Green] = 2;

		engine.State.Phase = TurnPhase.Recolor;
		engine.State.FinalTurnInProgress = true;
		human.HasFinishedRecolor = false;

		var greenBefore = human.Screen[GemColor.Green];
		var reserveBefore = engine.State.Reserve[GemColor.Green];

		Assert.True(engine.TryApply(
			new RecolorSectorCommand(0, upper.Card.InstanceId, "bl", GemColor.Green), out _));
		Assert.True(engine.TryApply(
			new RecolorSectorCommand(0, upper.Card.InstanceId, "br", GemColor.Green), out _));

		Assert.Equal(greenBefore - 2, human.Screen[GemColor.Green]);
		Assert.Equal(reserveBefore + 2, engine.State.Reserve[GemColor.Green]);
		Assert.Equal(GemColor.Green, upper.SectorOverrides["bl"]);
		Assert.Equal(GemColor.Green, upper.SectorOverrides["br"]);

		Assert.True(engine.TryApply(new ConfirmRecolorCommand(0), out _));
		Assert.Equal(TurnPhase.GameOver, engine.State.Phase);
		Assert.NotNull(engine.State.Result);
		Assert.True(engine.State.Result!.Scores[0].Circles > 0);
	}

	[Fact]
	public void Clear_recolor_returns_gem()
	{
		var engine = TestHelpers.NewSolo(seed: 3, virtuals: 0);
		var human = TestHelpers.Human(engine);
		human.Pyramid.Rows.Clear();
		human.Hand.Clear();
		human.Pyramid.PlaceStarter(new PyramidCard
		{
			Card = new CardInstance { InstanceId = 10, Kind = CardKind.Character, DefinitionId = 39 },
			Level = 1,
			Index = 0
		});
		foreach (GemColor c in Enum.GetValues<GemColor>())
			human.Screen[c] = 0;
		human.Screen[GemColor.Blue] = 1;
		engine.State.Phase = TurnPhase.Recolor;

		var card = human.Pyramid.AllCards.First();
		Assert.True(engine.TryApply(
			new RecolorSectorCommand(0, card.Card.InstanceId, "tl", GemColor.Blue), out _));
		Assert.Equal(0, human.Screen[GemColor.Blue]);

		Assert.True(engine.TryApply(
			new ClearSectorRecolorCommand(0, card.Card.InstanceId, "tl"), out _));
		Assert.Equal(1, human.Screen[GemColor.Blue]);
		Assert.Empty(card.SectorOverrides);
	}

	[Fact]
	public void CommandSerializer_roundtrips_recolor_commands()
	{
		GameCommand[] commands =
		[
			new RecolorSectorCommand(0, 12, "bl", GemColor.Yellow),
			new ClearSectorRecolorCommand(1, 9, "tr"),
			new ConfirmRecolorCommand(0)
		];
		foreach (var cmd in commands)
		{
			var back = CommandSerializer.Deserialize(CommandSerializer.Serialize(cmd));
			Assert.Equal(cmd, back);
		}
	}
}
