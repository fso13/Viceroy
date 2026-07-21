using System.Text.Json;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;

namespace Namestnik.Net;

/// <summary>Compact JSON mapping for <see cref="GameCommand"/> over RPC.</summary>
public static class CommandSerializer
{
	static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	public static string Serialize(GameCommand command)
	{
		object payload = command switch
		{
			SubmitAuctionBidCommand c => new
			{
				type = nameof(SubmitAuctionBidCommand),
				playerId = c.PlayerId,
				bid = SerializeBid(c.Bid)
			},
			PassAuctionCommand c => new { type = nameof(PassAuctionCommand), playerId = c.PlayerId },
			BidGemCommand c => new
			{
				type = nameof(BidGemCommand),
				playerId = c.PlayerId,
				color = c.Color.ToString()
			},
			BidAttackCommand c => new
			{
				type = nameof(BidAttackCommand),
				playerId = c.PlayerId,
				preferredCharacterId = c.PreferredCharacterId
			},
			ChooseAuctionCardCommand c => new
			{
				type = nameof(ChooseAuctionCardCommand),
				playerId = c.PlayerId,
				characterId = c.CharacterId
			},
			PlayCardCommand c => new
			{
				type = nameof(PlayCardCommand),
				playerId = c.PlayerId,
				handIndex = c.HandIndex,
				pyramidLevel = c.PyramidLevel,
				slotHint = c.SlotHint,
				lawTargetInstanceId = c.LawTargetInstanceId,
				extraHandIndices = c.ExtraHandIndices
			},
			DiscardForGemsCommand c => new
			{
				type = nameof(DiscardForGemsCommand),
				playerId = c.PlayerId,
				handIndex = c.HandIndex
			},
			PassDevelopmentCommand c => new
			{
				type = nameof(PassDevelopmentCommand),
				playerId = c.PlayerId
			},
			ChooseLevel5RewardCommand c => new
			{
				type = nameof(ChooseLevel5RewardCommand),
				playerId = c.PlayerId,
				takeFifteenVp = c.TakeFifteenVp
			},
			ResolveLawCommand c => new
			{
				type = nameof(ResolveLawCommand),
				playerId = c.PlayerId,
				optionIndex = c.OptionIndex,
				gemColor = c.GemColor?.ToString(),
				handIndices = c.HandIndices,
				characterDefinitionId = c.CharacterDefinitionId
			},
			ClaimPassGemsCommand c => new
			{
				type = nameof(ClaimPassGemsCommand),
				playerId = c.PlayerId,
				color = c.Color?.ToString(),
				confirm = c.Confirm
			},
			ChooseRewardCommand c => new
			{
				type = nameof(ChooseRewardCommand),
				playerId = c.PlayerId,
				optionIndex = c.OptionIndex
			},
			UndoCommand c => new { type = nameof(UndoCommand), playerId = c.PlayerId },
			ResolveTokenSwapCommand c => new
			{
				type = nameof(ResolveTokenSwapCommand),
				playerId = c.PlayerId,
				decline = c.Decline,
				ownCardInstanceId = c.OwnCardInstanceId,
				ownToken = c.OwnToken?.ToString(),
				otherPlayerId = c.OtherPlayerId,
				otherCardInstanceId = c.OtherCardInstanceId,
				otherToken = c.OtherToken?.ToString(),
				payGem = c.PayGem?.ToString(),
				confirm = c.Confirm
			},
			_ => throw new NotSupportedException($"Cannot serialize {command.GetType().Name}")
		};

		return JsonSerializer.Serialize(payload, Options);
	}

	public static GameCommand Deserialize(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var type = root.GetProperty("type").GetString()
			?? throw new JsonException("Command missing type");
		var playerId = root.GetProperty("playerId").GetInt32();

		return type switch
		{
			nameof(SubmitAuctionBidCommand) => new SubmitAuctionBidCommand(
				playerId, DeserializeBid(root.GetProperty("bid"))),
			nameof(PassAuctionCommand) => new PassAuctionCommand(playerId),
			nameof(BidGemCommand) => new BidGemCommand(
				playerId, Enum.Parse<GemColor>(root.GetProperty("color").GetString()!)),
			nameof(BidAttackCommand) => new BidAttackCommand(
				playerId, GetNullableInt(root, "preferredCharacterId")),
			nameof(ChooseAuctionCardCommand) => new ChooseAuctionCardCommand(
				playerId, root.GetProperty("characterId").GetInt32()),
			nameof(PlayCardCommand) => new PlayCardCommand(
				playerId,
				root.GetProperty("handIndex").GetInt32(),
				root.GetProperty("pyramidLevel").GetInt32(),
				root.GetProperty("slotHint").GetInt32(),
				GetNullableInt(root, "lawTargetInstanceId"),
				GetIntList(root, "extraHandIndices")),
			nameof(DiscardForGemsCommand) => new DiscardForGemsCommand(
				playerId, root.GetProperty("handIndex").GetInt32()),
			nameof(PassDevelopmentCommand) => new PassDevelopmentCommand(playerId),
			nameof(ChooseLevel5RewardCommand) => new ChooseLevel5RewardCommand(
				playerId, root.GetProperty("takeFifteenVp").GetBoolean()),
			nameof(ResolveLawCommand) => new ResolveLawCommand(
				playerId,
				GetNullableInt(root, "optionIndex"),
				GetNullableEnum<GemColor>(root, "gemColor"),
				GetIntList(root, "handIndices"),
				GetNullableInt(root, "characterDefinitionId")),
			nameof(ClaimPassGemsCommand) => new ClaimPassGemsCommand(
				playerId,
				GetNullableEnum<GemColor>(root, "color"),
				root.TryGetProperty("confirm", out var conf) && conf.GetBoolean()),
			nameof(ChooseRewardCommand) => new ChooseRewardCommand(
				playerId, root.GetProperty("optionIndex").GetInt32()),
			nameof(UndoCommand) => new UndoCommand(playerId),
			nameof(ResolveTokenSwapCommand) => new ResolveTokenSwapCommand(
				playerId,
				root.TryGetProperty("decline", out var dec) && dec.GetBoolean(),
				GetNullableInt(root, "ownCardInstanceId"),
				GetNullableEnum<TokenKind>(root, "ownToken"),
				GetNullableInt(root, "otherPlayerId"),
				GetNullableInt(root, "otherCardInstanceId"),
				GetNullableEnum<TokenKind>(root, "otherToken"),
				GetNullableEnum<GemColor>(root, "payGem"),
				root.TryGetProperty("confirm", out var conf2) && conf2.GetBoolean()),
			_ => throw new NotSupportedException($"Unknown command type: {type}")
		};
	}

	static object SerializeBid(AuctionBid bid) => bid switch
	{
		GemAuctionBid g => new { type = "gem", color = g.Color.ToString() },
		AttackAuctionBid a => new { type = "attack", preferredCharacterId = a.PreferredCharacterId },
		PassAuctionBid => new { type = "pass" },
		_ => throw new NotSupportedException($"Unknown bid {bid.GetType().Name}")
	};

	static AuctionBid DeserializeBid(JsonElement el)
	{
		var type = el.GetProperty("type").GetString();
		return type switch
		{
			"gem" => new GemAuctionBid(Enum.Parse<GemColor>(el.GetProperty("color").GetString()!)),
			"attack" => new AttackAuctionBid(GetNullableInt(el, "preferredCharacterId")),
			"pass" => new PassAuctionBid(),
			_ => throw new JsonException($"Unknown bid type: {type}")
		};
	}

	static int? GetNullableInt(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			return null;
		return el.GetInt32();
	}

	static TEnum? GetNullableEnum<TEnum>(JsonElement root, string name) where TEnum : struct, Enum
	{
		if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			return null;
		var s = el.GetString();
		return s is null ? null : Enum.Parse<TEnum>(s);
	}

	static IReadOnlyList<int>? GetIntList(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			return null;
		return el.EnumerateArray().Select(x => x.GetInt32()).ToArray();
	}
}
