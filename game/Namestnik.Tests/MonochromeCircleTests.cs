using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class MonochromeCircleTests
{
	[Fact]
	public void Placing_upper_card_that_forms_green_circle_grants_green_gem()
	{
		var engine = TestHelpers.NewSolo(seed: 12, virtuals: 0);
		TestHelpers.PassAuctionUntilDevelopment(engine);
		var human = TestHelpers.Human(engine);
		var db = TestHelpers.LoadCards();

		// Clear starter pyramid and rebuild: Vityaz | Rulevoy on L1, Skupshchik on L2.
		human.Pyramid.Rows.Clear();
		human.Hand.Clear();

		var vityaz = db.GetCharacter(39);
		var rulevoy = db.GetCharacter(51);
		var skup = db.GetCharacter(61);
		Assert.Equal(GemColor.Green, vityaz.Sectors.Tr);
		Assert.Equal(GemColor.Green, rulevoy.Sectors.Tl);
		Assert.Equal(GemColor.Green, skup.Sectors.Bl);
		Assert.Equal(GemColor.Green, skup.Sectors.Br);

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
			Index = 1 // append right
		});

		human.Hand.Add(new CardInstance { InstanceId = 3, Kind = CardKind.Character, DefinitionId = 61 });
		foreach (GemColor c in Enum.GetValues<GemColor>())
			human.Screen[c] = Math.Max(human.Screen[c], 4);
		engine.State.Reserve[GemColor.Green] = Math.Max(engine.State.Reserve[GemColor.Green], 2);

		var greenBefore = human.Screen[GemColor.Green];
		var placement = human.Pyramid.LegalPlacements().First(p => p.Level == 2);
		TestHelpers.Apply(engine, new PlayCardCommand(0, 0, placement.Level, placement.Index));

		Assert.Equal(greenBefore - CostGreenGems(skup, placement.Level) + 1, human.Screen[GemColor.Green]);
		Assert.Contains("одноцветный круг", engine.State.LastRewardSummary ?? "", StringComparison.Ordinal);
	}

	static int CostGreenGems(CharacterCard def, int level) =>
		def.CostToPlayAt(level).GetValueOrDefault(GemColor.Green);
}
