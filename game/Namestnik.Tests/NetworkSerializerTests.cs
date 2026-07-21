using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Namestnik.Net;
using Xunit;

namespace Namestnik.Tests;

public class NetworkSerializerTests
{
	[Fact]
	public void CommandSerializer_roundtrips_common_commands()
	{
		GameCommand[] commands =
		[
			new BidGemCommand(1, GemColor.Red),
			new PassAuctionCommand(0),
			new BidAttackCommand(1, PreferredCharacterId: 42),
			new ChooseLevel5RewardCommand(1, TakeFifteenVp: true),
			new ClaimPassGemsCommand(0, GemColor.Blue, Confirm: false),
			new ResolveLawCommand(1, OptionIndex: 2, GemColor: GemColor.Green)
		];

		foreach (var cmd in commands)
		{
			var json = CommandSerializer.Serialize(cmd);
			var back = CommandSerializer.Deserialize(json);
			Assert.Equal(cmd, back);
		}

		var play = new PlayCardCommand(0, 2, 1, 0, LawTargetInstanceId: 9, ExtraHandIndices: new[] { 1, 3 });
		var playBack = Assert.IsType<PlayCardCommand>(CommandSerializer.Deserialize(CommandSerializer.Serialize(play)));
		Assert.Equal(play.PlayerId, playBack.PlayerId);
		Assert.Equal(play.HandIndex, playBack.HandIndex);
		Assert.Equal(play.LawTargetInstanceId, playBack.LawTargetInstanceId);
		Assert.Equal(play.ExtraHandIndices, playBack.ExtraHandIndices);
	}

	[Fact]
	public void GameSnapshot_roundtrips_engine_state()
	{
		var engine = TestHelpers.NewSolo(seed: 123, virtuals: 1);
		TestHelpers.Apply(engine, new BidGemCommand(0, GemColor.Blue));

		var json = GameSnapshot.Serialize(engine.State);
		var restored = GameSnapshot.Deserialize(json);

		Assert.Equal(engine.State.Seed, restored.Seed);
		Assert.Equal(engine.State.Phase, restored.Phase);
		Assert.Equal(engine.State.Turn, restored.Turn);
		Assert.Equal(engine.State.Players.Count, restored.Players.Count);
		Assert.Equal(engine.State.SealedBids.Count, restored.SealedBids.Count);
		Assert.True(restored.SealedBids.ContainsKey(0));
		Assert.Equal(
			engine.State.GetPlayer(0).Hand.Count,
			restored.GetPlayer(0).Hand.Count);
		Assert.Equal(
			engine.State.GetPlayer(0).Screen[GemColor.Blue],
			restored.GetPlayer(0).Screen[GemColor.Blue]);

		engine.LoadState(restored);
		Assert.Equal(restored.Phase, engine.State.Phase);
	}
}
