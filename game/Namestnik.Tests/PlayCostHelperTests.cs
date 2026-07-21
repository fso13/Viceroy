using Namestnik.Core;
using Namestnik.Core.Models;
using Xunit;

namespace Namestnik.Tests;

public class PlayCostHelperTests
{
	[Fact]
	public void CanAfford_with_empty_screen_but_matching_infinite()
	{
		var def = MinimalCharacter(GemColor.Blue);
		var player = new PlayerState
		{
			PlayerId = 0,
			DisplayName = "Test",
			Role = SessionRole.LocalHuman
		};
		player.Pyramid.PlaceStarter(new PyramidCard
		{
			Card = new CardInstance
			{
				InstanceId = 1,
				DefinitionId = 1,
				Kind = CardKind.Character
			},
			Level = 1,
			Index = 0,
			InfiniteGem = GemColor.Blue
		});

		Assert.Equal(0, player.Screen.TotalGems);
		Assert.False(player.Screen.CanAfford(PlayCostHelper.BaseCost(def, 1)));
		Assert.True(PlayCostHelper.CanAfford(player, def, 1));
	}

	[Fact]
	public void CanAfford_false_when_infinite_already_used()
	{
		var def = MinimalCharacter(GemColor.Red);
		var player = new PlayerState
		{
			PlayerId = 0,
			DisplayName = "Test",
			Role = SessionRole.LocalHuman
		};
		player.Pyramid.PlaceStarter(new PyramidCard
		{
			Card = new CardInstance
			{
				InstanceId = 1,
				DefinitionId = 1,
				Kind = CardKind.Character
			},
			Level = 1,
			Index = 0,
			InfiniteGem = GemColor.Red,
			InfiniteUsedThisTurn = true
		});

		Assert.False(PlayCostHelper.CanAfford(player, def, 1));
	}

	static CharacterCard MinimalCharacter(GemColor costColor) => new()
	{
		Id = 1,
		Name = "Test",
		Sectors = new SectorColors
		{
			Tl = GemColor.Blue,
			Tr = GemColor.Red,
			Bl = GemColor.Green,
			Br = GemColor.Yellow
		},
		Levels = Enumerable.Range(1, 5)
			.Select(l => new LevelReward
			{
				Level = l,
				Cost = costColor,
				Reward = new VpReward(1)
			})
			.ToList()
	};
}
