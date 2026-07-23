namespace Namestnik.Core.Models;

/// <summary>Deep-clone helpers for undo / snapshots.</summary>
public static class GameStateClone
{
	public static GameState Clone(GameState src)
	{
		var dst = new GameState
		{
			Seed = src.Seed,
			Mode = src.Mode,
			HumanPlayerCount = src.HumanPlayerCount,
			VirtualOpponentCount = src.VirtualOpponentCount,
			Turn = src.Turn,
			Phase = src.Phase,
			AuctionRound = src.AuctionRound,
			AuctionSubPhase = src.AuctionSubPhase,
			DevelopmentRound = src.DevelopmentRound,
			DevelopmentSubPhase = src.DevelopmentSubPhase,
			NextInstanceId = src.NextInstanceId,
			NextTurnIsLast = src.NextTurnIsLast,
			FinalTurnInProgress = src.FinalTurnInProgress,
			LastRewardSummary = src.LastRewardSummary,
			Result = src.Result
		};

		CopyWallet(src.Reserve, dst.Reserve);
		CopyWallet(src.VirtualBox, dst.VirtualBox);
		dst.BigDeck.AddRange(src.BigDeck);
		dst.SmallDeck.AddRange(src.SmallDeck);
		dst.LawDeck.AddRange(src.LawDeck);
		dst.Discard.AddRange(src.Discard);

		for (var i = 0; i < src.AuctionSlots.Length; i++)
		{
			dst.AuctionSlots[i].CardAtTip = src.AuctionSlots[i].CardAtTip;
			dst.AuctionSlots[i].CardAtBase = src.AuctionSlots[i].CardAtBase;
		}

		foreach (var (id, bid) in src.SealedBids)
			dst.SealedBids[id] = bid;

		foreach (var choice in src.PendingCardChoices)
		{
			dst.PendingCardChoices.Add(new PendingCardChoice
			{
				PlayerId = choice.PlayerId,
				Color = choice.Color,
				Options = choice.Options.ToList()
			});
		}

		foreach (var (id, action) in src.SealedDevActions)
			dst.SealedDevActions[id] = action;

		if (src.PendingLevel5 is { } l5)
		{
			dst.PendingLevel5 = new PendingLevel5Choice
			{
				PlayerId = l5.PlayerId,
				PyramidCardInstanceId = l5.PyramidCardInstanceId,
				CharacterDefinitionId = l5.CharacterDefinitionId
			};
		}

		if (src.PendingLaw is { } law)
		{
			dst.PendingLaw = new PendingLawResolution
			{
				PlayerId = law.PlayerId,
				LawInstanceId = law.LawInstanceId,
				LawDefinitionId = law.LawDefinitionId,
				Kind = law.Kind,
				OptionLabels = law.OptionLabels.ToList(),
				DrawnInstanceId = law.DrawnInstanceId,
				AwaitingInfiniteColor = law.AwaitingInfiniteColor
			};
		}

		if (src.PendingRewardChoice is { } rc)
		{
			dst.PendingRewardChoice = new PendingRewardChoice
			{
				PlayerId = rc.PlayerId,
				HostInstanceId = rc.HostInstanceId,
				Options = rc.Options.ToList(),
				OptionLabels = rc.OptionLabels.ToList()
			};
		}

		if (src.PendingDeckDraw is { } dd)
		{
			dst.PendingDeckDraw = new PendingDeckDraw
			{
				PlayerId = dd.PlayerId,
				Remaining = dd.Remaining
			};
		}

		if (src.PendingPassGems is { } pg)
		{
			dst.PendingPassGems = new PendingPassGems
			{
				PlayerId = pg.PlayerId,
				Amount = pg.Amount,
				Picked = pg.Picked.ToList()
			};
		}

		if (src.PendingTokenSwap is { } ts)
		{
			dst.PendingTokenSwap = new PendingTokenSwap
			{
				PlayerId = ts.PlayerId,
				LawInstanceId = ts.LawInstanceId,
				OwnCardInstanceId = ts.OwnCardInstanceId,
				OwnToken = ts.OwnToken,
				OtherPlayerId = ts.OtherPlayerId,
				OtherCardInstanceId = ts.OtherCardInstanceId,
				OtherToken = ts.OtherToken
			};
			dst.PendingTokenSwap.Payment.AddRange(ts.Payment);
		}

		dst.DeferredDevPlays.AddRange(src.DeferredDevPlays);
		dst.DeferredPassers.AddRange(src.DeferredPassers);

		foreach (var p in src.Players)
			dst.Players.Add(ClonePlayer(p));

		return dst;
	}

	static PlayerState ClonePlayer(PlayerState src)
	{
		var dst = new PlayerState
		{
			PlayerId = src.PlayerId,
			DisplayName = src.DisplayName,
			Role = src.Role,
			HasPassedAuction = src.HasPassedAuction,
			HasPassedDevelopment = src.HasPassedDevelopment,
			AcquiredAuctionCardThisTurn = src.AcquiredAuctionCardThisTurn,
			ActedThisDevelopmentRound = src.ActedThisDevelopmentRound,
			SkipNextAuction = src.SkipNextAuction,
			HasFinishedRecolor = src.HasFinishedRecolor
		};
		CopyWallet(src.Screen, dst.Screen);
		foreach (var card in src.Hand)
			dst.Hand.Add(CloneCard(card));
		ClonePyramid(src.Pyramid, dst.Pyramid);
		return dst;
	}

	static void ClonePyramid(Pyramid src, Pyramid dst)
	{
		foreach (var (level, row) in src.Rows)
		{
			dst.Rows[level] = row.Select(ClonePyramidCard).ToList();
		}
	}

	static PyramidCard ClonePyramidCard(PyramidCard src)
	{
		var dst = new PyramidCard
		{
			Card = CloneCard(src.Card),
			Level = src.Level,
			Index = src.Index,
			VictoryPoints = src.VictoryPoints,
			Science = src.Science,
			Magic = src.Magic,
			Defense = src.Defense,
			InfiniteGem = src.InfiniteGem,
			InfiniteUsedThisTurn = src.InfiniteUsedThisTurn,
			BonusMagic = src.BonusMagic,
			ParkedGems = src.ParkedGems
		};
		dst.BonusCircles.AddRange(src.BonusCircles);
		foreach (var t in src.TuckedCards)
			dst.TuckedCards.Add(CloneCard(t));
		foreach (var (k, v) in src.SectorOverrides)
			dst.SectorOverrides[k] = v;
		return dst;
	}

	static CardInstance CloneCard(CardInstance src) => new()
	{
		InstanceId = src.InstanceId,
		Kind = src.Kind,
		DefinitionId = src.DefinitionId
	};

	static void CopyWallet(GemWallet src, GemWallet dst)
	{
		foreach (GemColor color in Enum.GetValues<GemColor>())
			dst[color] = src[color];
		dst.AttackTokens = src.AttackTokens;
	}
}
