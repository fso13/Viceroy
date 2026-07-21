using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class PyramidSupportTests
{
	[Fact]
	public void LegalPlacements_skips_gaps_so_upper_card_cannot_hang()
	{
		var pyramid = new Pyramid();
		// L1: five cards (0..4)
		pyramid.PlaceStarter(Make(1, 1, 0));
		for (var i = 1; i < 5; i++)
			pyramid.Place(Make(i + 1, 1, i)); // append right

		// L2 only on far left (0-1) and far right (3-4) — gap at column 2
		pyramid.Place(Make(10, 2, 0));
		pyramid.Place(Make(11, 2, 3));

		var legal = pyramid.LegalPlacements();
		Assert.DoesNotContain(legal, p => p is { Level: 3, Index: 0 });
		Assert.DoesNotContain(legal, p => p.Level == 3);

		Assert.Throws<InvalidOperationException>(() => pyramid.Place(Make(20, 3, 0)));
	}

	[Fact]
	public void LegalPlacements_allows_upper_only_on_adjacent_pair()
	{
		var pyramid = new Pyramid();
		pyramid.PlaceStarter(Make(1, 1, 0));
		pyramid.Place(Make(2, 1, 1));
		pyramid.Place(Make(3, 1, 2));

		pyramid.Place(Make(10, 2, 0));
		pyramid.Place(Make(11, 2, 1));

		var legal = pyramid.LegalPlacements();
		Assert.Contains(legal, p => p is { Level: 3, Index: 0 });
		pyramid.Place(Make(20, 3, 0));
		Assert.NotNull(pyramid.SupportsOf(pyramid.Rows[3][0]));
	}

	static PyramidCard Make(int instanceId, int level, int index) => new()
	{
		Card = new CardInstance
		{
			InstanceId = instanceId,
			Kind = CardKind.Character,
			DefinitionId = instanceId
		},
		Level = level,
		Index = index
	};
}
