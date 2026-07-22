using Godot;
using Namestnik.Core;
using Namestnik.Core.Commands;
using Namestnik.Core.Models;
using Namestnik.Net;

namespace Namestnik.Ui;

public partial class GameScreenController : Control
{
	Label _phaseLabel = null!;
	Label _infoLabel = null!;
	Label _pyramidStatsLabel = null!;
	HBoxContainer _gemStatusRow = null!;
	VBoxContainer _pyramidCards = null!;
	HBoxContainer _auctionCards = null!;
	RichTextLabel _log = null!;
	HBoxContainer _bidRow = null!;
	HBoxContainer _choiceRow = null!;
	HBoxContainer _passGemsRow = null!;
	HBoxContainer _rewardRow = null!;
	HBoxContainer _tokenSwapRow = null!;
	HBoxContainer _handBox = null!;
	HBoxContainer _level5Row = null!;
	HBoxContainer _lawPromptRow = null!;
	Button _passAuctionButton = null!;
	Button _passDevButton = null!;
	Button _attackButton = null!;
	Button _undoButton = null!;
	Button _cancelBuildButton = null!;
	CardZoomOverlay _zoom = null!;
	NoticeOverlay _notice = null!;
	ActionModalOverlay _actionModal = null!;

	readonly HashSet<int> _lawReturnSelection = new();
	readonly HashSet<int> _law72TuckSelection = new();

	/// <summary>Hand index selected for multi-step play (placement / law target).</summary>
	int? _selectedHandIndex;

	/// <summary>True while a hand card is selected and awaiting placement/target.</summary>
	bool _buildMode;

	static Vector2 ThumbAuction = new(88, 88);
	static Vector2 ThumbPyramid = new(100, 100);
	static Vector2 ThumbHand = new(110, 110);
	const int CardGap = 4;

	ScrollContainer _handScroll = null!;
	HBoxContainer _tableRow = null!;
	bool _resizeRefreshQueued;

	public override void _Ready()
	{
		_phaseLabel = GetNode<Label>("%PhaseLabel");
		_infoLabel = GetNode<Label>("%InfoLabel");
		_pyramidStatsLabel = GetNode<Label>("%PyramidStatsLabel");
		_gemStatusRow = GetNode<HBoxContainer>("%GemStatusRow");
		_pyramidCards = GetNode<VBoxContainer>("%PyramidCards");
		_auctionCards = GetNode<HBoxContainer>("%AuctionCards");
		_log = GetNode<RichTextLabel>("%Log");
		_bidRow = GetNode<HBoxContainer>("%BidRow");
		_choiceRow = GetNode<HBoxContainer>("%ChoiceRow");
		_passGemsRow = GetNode<HBoxContainer>("%PassGemsRow");
		_rewardRow = GetNode<HBoxContainer>("%RewardRow");
		_tokenSwapRow = GetNode<HBoxContainer>("%TokenSwapRow");
		_handBox = GetNode<HBoxContainer>("%HandBox");
		_handScroll = _handBox.GetParent<ScrollContainer>();
		_tableRow = GetNode<HBoxContainer>("Margin/VBox/TableRow");
		_level5Row = GetNode<HBoxContainer>("%Level5Row");
		_lawPromptRow = GetNode<HBoxContainer>("%LawPromptRow");
		_passAuctionButton = GetNode<Button>("%PassAuctionButton");
		_passDevButton = GetNode<Button>("%PassDevButton");
		_attackButton = GetNode<Button>("%AttackButton");
		_undoButton = GetNode<Button>("%UndoButton");
		_cancelBuildButton = GetNode<Button>("%CancelBuildButton");

		_passAuctionButton.Pressed += OnPassAuction;
		_passDevButton.Pressed += OnPassDev;
		_attackButton.Pressed += OnAttack;
		_undoButton.Pressed += OnUndo;
		_cancelBuildButton.Pressed += OnCancelBuild;
		GetNode<Button>("%MenuButton").Pressed += OnMenu;
		GetNode<Button>("%Level5Bundle").Pressed += () => ChooseLevel5(false);
		GetNode<Button>("%Level5Fifteen").Pressed += () => ChooseLevel5(true);

		_zoom = new CardZoomOverlay();
		AddChild(_zoom);
		_notice = new NoticeOverlay();
		AddChild(_notice);
		_actionModal = new ActionModalOverlay();
		AddChild(_actionModal);

		_handBox.AddThemeConstantOverride("separation", CardGap);
		_auctionCards.AddThemeConstantOverride("separation", CardGap);
		Resized += OnGameResized;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.SessionEvent += AppendLog;
		session.NoticeRequested += OnNoticeRequested;
		UpdateThumbMetrics();
		CallDeferred(MethodName.Refresh);
	}

	void OnGameResized()
	{
		if (_resizeRefreshQueued)
			return;
		_resizeRefreshQueued = true;
		CallDeferred(MethodName.ApplyResizeRefresh);
	}

	void ApplyResizeRefresh()
	{
		_resizeRefreshQueued = false;
		var prevAuction = ThumbAuction;
		var prevHand = ThumbHand;
		UpdateThumbMetrics();
		if (ThumbAuction == prevAuction && ThumbHand == prevHand)
			return;
		var session = GetNodeOrNull<GameSessionAutoload>("/root/GameSession");
		if (session?.Session is not null)
			Refresh();
	}

	/// <summary>Size thumbs so auction (2 rows) + hand fit the left column without overflow.</summary>
	void UpdateThumbMetrics()
	{
		var tableH = _tableRow.Size.Y;
		if (tableH < 40f)
			tableH = Mathf.Max(360f, Size.Y * 0.62f);

		var auction = Mathf.Clamp(Mathf.Round(tableH * 0.20f), 70f, 96f);
		var hand = Mathf.Clamp(Mathf.Round(tableH * 0.26f), 88f, 120f);
		ThumbAuction = new Vector2(auction, auction);
		ThumbHand = new Vector2(hand, hand);
		_handScroll.CustomMinimumSize = new Vector2(0, hand + 28f);
	}

	void OnNoticeRequested(string title, string body)
	{
		_notice.Enqueue(title, body);
		Refresh();
	}

	void BindInspect(
		CardThumb thumb,
		CardDatabase? cards,
		CardKind kind,
		int definitionId,
		string title,
		string details) =>
		thumb.EnableInspect(() => _zoom.ShowCard(cards, kind, definitionId, title, details));

	void ClearBuildDraft()
	{
		_selectedHandIndex = null;
		_buildMode = false;
		_law72TuckSelection.Clear();
	}

	void SelectHandCard(int handIndex)
	{
		_selectedHandIndex = handIndex;
		_buildMode = true;
		_law72TuckSelection.Clear();
		Refresh();
	}

	void Refresh()
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var engine = session.Session?.Engine;
		if (engine is null)
		{
			_phaseLabel.Text = session.ActiveMode == GameMode.Client
				? "Клиент (ожидание снапшота от хоста)"
				: "Нет активной партии";
			_infoLabel.Text = $"Режим: {session.ActiveMode}";
			ClearContainer(_pyramidCards);
			ClearContainer(_auctionCards);
			ClearContainer(_handBox);
			SetAuctionUi(false, false);
			_handBox.Visible = false;
			_level5Row.Visible = false;
			_lawPromptRow.Visible = false;
			_actionModal.HideModal();
			_passGemsRow.Visible = false;
			_rewardRow.Visible = false;
			_tokenSwapRow.Visible = false;
			_cancelBuildButton.Visible = false;
			UpdateUndoButton(session);
			return;
		}

		var s = engine.State;
		var cards = session.Cards;
		if (s.Phase == TurnPhase.GameOver && s.Result is { } result)
		{
			_phaseLabel.Text = result.IsDraw ? "Ничья" : $"Победитель: {result.Scores.First(x => result.WinnerIds.Contains(x.PlayerId)).DisplayName}";
			_infoLabel.Text = string.Join("\n", result.Scores.Select(sc => sc.ToString()));
			ClearContainer(_pyramidCards);
			ClearContainer(_auctionCards);
			SetAuctionUi(false, false);
			_handBox.Visible = false;
			_level5Row.Visible = false;
			_lawPromptRow.Visible = false;
			_actionModal.HideModal();
			_passGemsRow.Visible = false;
			_rewardRow.Visible = false;
			_tokenSwapRow.Visible = false;
			_cancelBuildButton.Visible = false;
			_passDevButton.Disabled = true;
			ClearBuildDraft();
			UpdateUndoButton(session);
			return;
		}

		_phaseLabel.Text = $"Ход {s.Turn}/{GameState.MaxTurns} — {s.Phase}" +
			(s.FinalTurnInProgress ? " [ФИНАЛ]" : "") +
			(s.Phase == TurnPhase.Auction
				? $" / круг {(int)s.AuctionRound} / {s.AuctionSubPhase}"
				: "") +
			(s.Phase == TurnPhase.Development
				? $" / раунд {s.DevelopmentRound} / {s.DevelopmentSubPhase}"
				: "");

		var localId = session.Session!.LocalPlayerId;
		var local = s.GetPlayer(localId);

		var sealedMark = s.Phase == TurnPhase.Auction
			? (s.SealedBids.ContainsKey(localId) ? "ставка принята" : "ждём ставку")
			: (s.SealedDevActions.ContainsKey(localId) || local.ActedThisDevelopmentRound
				? "действие принято"
				: "ждём действие");

		_infoLabel.Text =
			$"{local.DisplayName} | рука:{local.Hand.Count} | пирамида:{local.Pyramid.AllCards.Count()} " +
			$"(VP:{local.VictoryPointTokens} Sci:{local.ScienceTokens} Mag:{local.MagicTokens} Def:{local.DefenseTokens}) · " +
			$"колоды {s.BigDeck.Count}/{s.SmallDeck.Count}/{s.LawDeck.Count} · {sealedMark}" +
			(string.IsNullOrEmpty(s.LastRewardSummary) ? "" : $" · ★ {s.LastRewardSummary}");

		RebuildGemStatusRow(local);
		UpdateThumbMetrics();


		var canBid = s.Phase == TurnPhase.Auction
			&& s.AuctionSubPhase == AuctionSubPhase.CollectingBids
			&& !local.HasPassedAuction
			&& !local.AcquiredAuctionCardThisTurn
			&& !s.SealedBids.ContainsKey(localId);

		var canChoose = s.Phase == TurnPhase.Auction
			&& s.AuctionSubPhase == AuctionSubPhase.ChoosingCards
			&& s.HasPendingChoice(localId);

		var canPassGems = s.Phase == TurnPhase.Auction
			&& s.AuctionSubPhase == AuctionSubPhase.ClaimingPassGems
			&& s.PendingPassGems?.PlayerId == localId;

		var canDev = s.Phase == TurnPhase.Development
			&& s.DevelopmentSubPhase == DevelopmentSubPhase.CollectingActions
			&& !local.HasPassedDevelopment
			&& !local.ActedThisDevelopmentRound
			&& !s.SealedDevActions.ContainsKey(localId);

		var canLevel5 = s.Phase == TurnPhase.Development
			&& s.DevelopmentSubPhase == DevelopmentSubPhase.ChoosingLevel5Reward
			&& s.PendingLevel5?.PlayerId == localId;

		var canReward = s.Phase == TurnPhase.Development
			&& s.DevelopmentSubPhase == DevelopmentSubPhase.ChoosingReward
			&& s.PendingRewardChoice?.PlayerId == localId;

		var canDeckDraw = s.Phase == TurnPhase.Development
			&& s.DevelopmentSubPhase == DevelopmentSubPhase.ChoosingDeckDraw
			&& s.PendingDeckDraw?.PlayerId == localId;

		var canLaw = s.Phase == TurnPhase.Development
			&& s.DevelopmentSubPhase == DevelopmentSubPhase.ResolvingLaw
			&& s.PendingLaw?.PlayerId == localId;

		var canTokenSwap = s.Phase == TurnPhase.Development
			&& s.PendingTokenSwap?.PlayerId == localId
			&& s.PendingLaw is null;

		if (!canDev)
		{
			ClearBuildDraft();
			_pendingTuckFreeDrop = null;
		}

		RebuildAuctionBoard(s, cards, local, canBid);
		RebuildPyramidBoard(local, cards, canDev);

		SetAuctionUi(canBid, canChoose);
		_passDevButton.Disabled = !canDev;
		_handBox.Visible = true;
		_level5Row.Visible = canLevel5;
		_lawPromptRow.Visible = false;
		_passGemsRow.Visible = canPassGems;
		_rewardRow.Visible = canReward;
		_tokenSwapRow.Visible = canTokenSwap || (s.PendingTokenSwap?.PlayerId == localId);
		_cancelBuildButton.Visible = canDev && _buildMode;

		RebuildChoiceButtons(canChoose ? s.PendingCardChoices.First(c => c.PlayerId == localId) : null, cards);
		RebuildPassGems(canPassGems ? s.PendingPassGems : null);
		RebuildRewardChoice(canReward ? s.PendingRewardChoice : null);
		RebuildHand(local, cards, canDev);
		RebuildLawPrompt(canLaw ? s : null, local, cards);
		RebuildDeckDrawPrompt(canDeckDraw ? s.PendingDeckDraw : null, s);
		RebuildTokenSwap(
			s.PendingTokenSwap?.PlayerId == localId ? s : null,
			local,
			cards,
			swapActive: canTokenSwap);
		UpdateUndoButton(session);
	}

	void RebuildGemStatusRow(PlayerState local)
	{
		ClearContainer(_gemStatusRow);
		_gemStatusRow.AddChild(new Label { Text = "За ширмой:" });
		foreach (GemColor color in Enum.GetValues<GemColor>())
		{
			_gemStatusRow.AddChild(GemIcons.MakeRect(color, 22f));
			_gemStatusRow.AddChild(new Label
			{
				Text = $"×{local.Screen[color]}",
				VerticalAlignment = VerticalAlignment.Center
			});
		}

		_gemStatusRow.AddChild(new Label { Text = $"  атака ×{local.Screen.AttackTokens}" });
	}

	void UpdateUndoButton(GameSessionAutoload session)
	{
		var solo = session.ActiveMode == GameMode.Solo;
		var canUndo = solo && (session.Session?.CanUndo == true
			|| session.Session?.Engine?.CanUndo == true);
		_undoButton.Visible = solo && canUndo;
		_undoButton.Disabled = !canUndo;
	}

	void SetAuctionUi(bool canBid, bool canChoose)
	{
		_bidRow.Visible = canBid || canChoose;
		_passAuctionButton.Disabled = !canBid;
		_attackButton.Disabled = !canBid;
		_choiceRow.Visible = canChoose;
	}

	static void ClearContainer(Node node)
	{
		foreach (var child in node.GetChildren())
			child.QueueFree();
	}

	/// <summary>After dropping law #74 on a slot, wait for free-card pick.</summary>
	(int HandIndex, int Level, int Index)? _pendingTuckFreeDrop;

	void RebuildPyramidBoard(PlayerState local, CardDatabase? cards, bool canDev)
	{
		ClearContainer(_pyramidCards);
		_pyramidStatsLabel.Text = PyramidStats.Format(local);

		var cardCount = Math.Max(1, local.Pyramid.AllCards.Count());
		var height = Math.Max(1, local.Pyramid.Height);
		var baseCountHint = Math.Max(1, local.Pyramid.BaseCount);
		// Prefer large cards; shrink when the base row would overflow the column.
		var size = height >= 5 || cardCount > 14 ? 72f
			: cardCount <= 4 ? 110f
			: cardCount <= 8 ? 100f
			: cardCount <= 12 ? 88f
			: 78f;
		var availW = _pyramidCards.Size.X;
		if (availW < 40f)
			availW = Mathf.Max(280f, Size.X * 0.48f);
		var maxByWidth = (availW - 24f) / Math.Max(1, baseCountHint + (canDev ? 1 : 0));
		size = Mathf.Clamp(Mathf.Min(size, maxByWidth), 56f, 120f);
		ThumbPyramid = new Vector2(size, size);

		var legal = canDev ? local.Pyramid.LegalPlacements() : new List<(int Level, int Index)>();
		var legalSet = legal.ToHashSet();
		var maxLevel = Math.Max(1, Math.Max(local.Pyramid.Height, legal.Count == 0 ? 1 : legal.Max(x => x.Level)));

		if (canDev)
		{
			_pyramidCards.AddChild(new Label
			{
				Text = "Перетащите карту из руки на зелёный слот"
			});
		}

		if (_pendingTuckFreeDrop is { } pending && canDev)
			RebuildTuckFreePicker(local, cards, pending);

		// Brick layout on a shared base grid:
		// level L slot i is centered on the seam between level L-1 slots i and i+1.
		var cell = CardThumb.OuterSize(ThumbPyramid, reserveCaption: false);
		var hGap = Mathf.Max(2, (int)(ThumbPyramid.X * 0.02f));
		var vGap = hGap;
		var pitch = cell.X + hGap;
		var baseCount = Math.Max(1, local.Pyramid.BaseCount);
		var baseWidth = baseCount * cell.X + Math.Max(0, baseCount - 1) * hGap;

		var stack = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		stack.AddThemeConstantOverride("separation", vGap);

		for (var level = maxLevel; level >= 2; level--)
		{
			var rowWrap = new MarginContainer
			{
				ZIndex = level,
				CustomMinimumSize = new Vector2(baseWidth, cell.Y)
			};
			var leftPad = (int)Math.Round((level - 1) * pitch * 0.5f);
			rowWrap.AddThemeConstantOverride("margin_left", leftPad);
			var rowBox = new HBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Begin,
				SizeFlagsHorizontal = Control.SizeFlags.Fill
			};
			rowBox.AddThemeConstantOverride("separation", hGap);
			BuildUpperRow(rowBox, local, cards, canDev, legalSet, level, cell, baseCount);
			rowWrap.AddChild(rowBox);
			stack.AddChild(rowWrap);
		}

		var baseCards = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
			ZIndex = 1,
			CustomMinimumSize = new Vector2(baseWidth, cell.Y)
		};
		baseCards.AddThemeConstantOverride("separation", hGap);
		BuildBaseCards(baseCards, local, cards, canDev, legalSet, cell);
		stack.AddChild(baseCards);

		// Side L1 drops sit beside the stack, pinned to the base row (bottom).
		var board = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		board.AddThemeConstantOverride("separation", hGap);
		if (canDev && legalSet.Contains((1, 0)))
		{
			var left = MakeDropSlot(local, cards, 1, 0, baseCount == 0 ? "Старт" : "← L1", cell);
			left.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			left.ZIndex = 1;
			board.AddChild(left);
		}

		board.AddChild(stack);
		if (canDev && local.Pyramid.BaseCount > 0 && legalSet.Contains((1, local.Pyramid.BaseCount)))
		{
			var right = MakeDropSlot(local, cards, 1, local.Pyramid.BaseCount, "L1 →", cell);
			right.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			right.ZIndex = 1;
			board.AddChild(right);
		}

		_pyramidCards.AddChild(board);
	}

	void BuildBaseCards(
		HBoxContainer rowBox,
		PlayerState local,
		CardDatabase? cards,
		bool canDev,
		HashSet<(int Level, int Index)> legalSet,
		Vector2 cell)
	{
		if (local.Pyramid.Rows.TryGetValue(1, out var row))
		{
			foreach (var pc in row.OrderBy(c => c.Index))
				rowBox.AddChild(MakePyramidCardThumb(local, cards, canDev, pc));
		}
		else if (!canDev || !legalSet.Contains((1, 0)))
		{
			rowBox.AddChild(new Label { Text = "(пусто)" });
		}
	}

	void BuildUpperRow(
		HBoxContainer rowBox,
		PlayerState local,
		CardDatabase? cards,
		bool canDev,
		HashSet<(int Level, int Index)> legalSet,
		int level,
		Vector2 cell,
		int baseCount)
	{
		if (!local.Pyramid.Rows.TryGetValue(level - 1, out var below) || below.Count < 2)
		{
			rowBox.AddChild(new Label { Text = "(нужны 2 карты ниже)" });
			return;
		}

		var existingByVisual = local.Pyramid.Rows.TryGetValue(level, out var upper)
			? upper.ToDictionary(c => c.Index)
			: new Dictionary<int, PyramidCard>();

		var legalByVisual = new HashSet<int>();
		foreach (var (lvl, idx) in legalSet)
		{
			if (lvl == level)
				legalByVisual.Add(idx);
		}

		var slotCount = Math.Max(1, baseCount - level + 1);
		for (var visual = 0; visual < slotCount; visual++)
		{
			if (existingByVisual.TryGetValue(visual, out var pc))
			{
				rowBox.AddChild(MakePyramidCardThumb(local, cards, canDev, pc));
			}
			else if (canDev && legalByVisual.Contains(visual))
			{
				rowBox.AddChild(MakeDropSlot(local, cards, level, visual, $"L{level}", cell));
			}
			else
			{
				rowBox.AddChild(new Control
				{
					CustomMinimumSize = cell,
					SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
					SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
				});
			}
		}
	}

	CardThumb MakePyramidCardThumb(PlayerState local, CardDatabase? cards, bool canDev, PyramidCard pc)
	{
		var tip = cards is null
			? $"#{pc.Card.DefinitionId}"
			: CardTooltips.ForPyramidCard(cards, pc);
		var thumb = new CardThumb();
		var name = CardShortName(pc.Card, cards);
		thumb.SetupCard(
			cards,
			pc.Card.Kind,
			pc.Card.DefinitionId,
			name,
			tip,
			ThumbPyramid,
			reserveCaption: false);
		thumb.ZIndex = pc.Level;
		thumb.SetTokenBadges(PyramidStats.TokenBadges(pc));
		BindInspect(thumb, cards, pc.Card.Kind, pc.Card.DefinitionId, name, tip);

		if (canDev)
		{
			thumb.EnableCardDrop(
				handIndex => CanDropOnPyramidCard(local, cards, handIndex, pc),
				handIndex => DropOnPyramidCard(local, handIndex, pc));
		}

		return thumb;
	}

	PyramidDropSlot MakeDropSlot(
		PlayerState local,
		CardDatabase? cards,
		int level,
		int index,
		string caption,
		Vector2? cell = null)
	{
		var slot = new PyramidDropSlot();
		var tip = $"Слот ур.{level}" + (level == 1
			? (index == 0 ? " (слева)" : " (справа)")
			: $" (опора {index})");
		slot.Configure(
			level,
			index,
			caption,
			cell ?? CardThumb.OuterSize(ThumbPyramid, reserveCaption: false),
			tip,
			handIndex => CanDropOnSlot(local, cards, handIndex, level),
			handIndex => DropOnSlot(local, cards, handIndex, level, index));
		return slot;
	}

	bool CanDropOnSlot(PlayerState local, CardDatabase? cards, int handIndex, int level)
	{
		if (handIndex < 0 || handIndex >= local.Hand.Count)
			return false;
		var card = local.Hand[handIndex];
		if (card.Kind == CardKind.Law && level == 5)
			return false;
		if (card.Kind == CardKind.Law && LawIds.NeedsTargetOnPlay(card.DefinitionId)
		    && card.DefinitionId is LawIds.Replace or LawIds.TuckUnderCharacter)
			return false; // drop onto a card instead
		if (card.Kind == CardKind.Character && cards is not null)
		{
			var def = cards.GetCharacter(card.DefinitionId);
			if (!PlayCostHelper.CanAfford(local, def, level))
				return false;
		}

		return true;
	}

	bool CanDropOnPyramidCard(PlayerState local, CardDatabase? cards, int handIndex, PyramidCard target)
	{
		if (handIndex < 0 || handIndex >= local.Hand.Count)
			return false;
		var card = local.Hand[handIndex];
		if (card.Kind != CardKind.Law)
			return false;
		if (card.DefinitionId == LawIds.Replace)
			return true;
		if (card.DefinitionId == LawIds.TuckUnderCharacter)
			return target.Card.Kind == CardKind.Character;
		if (card.DefinitionId == LawIds.TuckFreeCard && _pendingTuckFreeDrop is not null)
			return local.Pyramid.IsFree(target);
		return false;
	}

	void DropOnSlot(PlayerState local, CardDatabase? cards, int handIndex, int level, int index)
	{
		if (!CanDropOnSlot(local, cards, handIndex, level))
			return;

		var card = local.Hand[handIndex];
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;

		if (card.Kind == CardKind.Law && card.DefinitionId == LawIds.TuckFreeCard)
		{
			_pendingTuckFreeDrop = (handIndex, level, index);
			ClearBuildDraft();
			Refresh();
			return;
		}

		IReadOnlyList<int>? extras = null;
		if (card.Kind == CardKind.Law && card.DefinitionId == LawIds.TuckFromHand)
			extras = _law72TuckSelection.Where(x => x != handIndex).ToList();

		_pendingTuckFreeDrop = null;
		ClearBuildDraft();
		session.Submit(new PlayCardCommand(pid, handIndex, level, index, ExtraHandIndices: extras));
		Refresh();
	}

	void DropOnPyramidCard(PlayerState local, int handIndex, PyramidCard target)
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;
		var card = local.Hand[handIndex];

		if (card.DefinitionId == LawIds.TuckFreeCard && _pendingTuckFreeDrop is { } pending)
		{
			if (pending.HandIndex != handIndex)
				return;
			if (!local.Pyramid.IsFree(target))
				return;
			session.Submit(new PlayCardCommand(
				pid,
				pending.HandIndex,
				pending.Level,
				pending.Index,
				LawTargetInstanceId: target.Card.InstanceId));
			_pendingTuckFreeDrop = null;
			ClearBuildDraft();
			Refresh();
			return;
		}

		if (card.DefinitionId is not (LawIds.Replace or LawIds.TuckUnderCharacter))
			return;
		if (card.DefinitionId == LawIds.TuckUnderCharacter && target.Card.Kind != CardKind.Character)
			return;

		_pendingTuckFreeDrop = null;
		ClearBuildDraft();
		session.Submit(new PlayCardCommand(
			pid, handIndex, 1, 0, LawTargetInstanceId: target.Card.InstanceId));
		Refresh();
	}

	void RebuildTuckFreePicker(PlayerState local, CardDatabase? cards, (int HandIndex, int Level, int Index) pending)
	{
		var box = new HBoxContainer();
		box.AddChild(new Label { Text = $"#{LawIds.TuckFreeCard}: выберите свободную карту или перетащите закон на неё:" });
		foreach (var free in local.Pyramid.FreeCards())
		{
			var name = CardShortName(free.Card, cards);
			var fid = free.Card.InstanceId;
			var btn = new Button { Text = $"↓{name}" };
			btn.Pressed += () =>
			{
				var session = GetNode<GameSessionAutoload>("/root/GameSession");
				session.Submit(new PlayCardCommand(
					session.Session?.LocalPlayerId ?? 0,
					pending.HandIndex,
					pending.Level,
					pending.Index,
					LawTargetInstanceId: fid));
				_pendingTuckFreeDrop = null;
				ClearBuildDraft();
				Refresh();
			};
			box.AddChild(btn);
		}

		var cancel = new Button { Text = "Отмена слота" };
		cancel.Pressed += () =>
		{
			_pendingTuckFreeDrop = null;
			Refresh();
		};
		box.AddChild(cancel);
		_pyramidCards.AddChild(box);
	}

	void RebuildAuctionBoard(GameState s, CardDatabase? cards, PlayerState local, bool canBid)
	{
		ClearContainer(_auctionCards);
		foreach (var slot in s.AuctionSlots)
		{
			var col = new VBoxContainer
			{
				Alignment = BoxContainer.AlignmentMode.Begin,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
			};
			col.AddThemeConstantOverride("separation", 2);

			CardThumb MakeAuctionThumb(int? id, string emptyTag)
			{
				if (id is not int cid)
				{
					var empty = new CardThumb();
					empty.SetupBack(emptyTag, "Пусто", ThumbAuction);
					empty.Modulate = new Color(1, 1, 1, 0.35f);
					return empty;
				}

				var tip = cards is null
					? $"#{cid}"
					: CardTooltips.ForCard(cards, CardKind.Character, cid);
				var name = CardShortName(
					new CardInstance { InstanceId = 0, Kind = CardKind.Character, DefinitionId = cid },
					cards);
				var thumb = new CardThumb();
				thumb.SetupCard(cards, CardKind.Character, cid, name, tip, ThumbAuction);
				BindInspect(thumb, cards, CardKind.Character, cid, name, tip);
				return thumb;
			}

			// Arrow points down: основание сверху, остриё снизу.
			// Конец аукциона: низ (остриё) → сброс, верх сдвигается вниз, новые 4 сверху.
			col.AddChild(MakeAuctionThumb(slot.CardAtBase, "осн."));

			var color = slot.Color;
			var hasCards = slot.AvailableCount > 0;
			var canAfford = local.Screen.CanPay(color);
			var bidEnabled = canBid && hasCards && canAfford;
			var gemSize = Mathf.Clamp(ThumbAuction.X * 0.38f, 28f, 40f);
			var gemBtn = new Button
			{
				Icon = GemIcons.Get(color),
				ExpandIcon = true,
				Text = "",
				TooltipText = bidEnabled
					? $"Ставка: {GemIcons.ColorName(color)}"
					: !canBid
						? $"Рынок: {GemIcons.ColorName(color)}"
						: !hasCards
							? $"{GemIcons.ColorName(color)}: нет карт"
							: $"{GemIcons.ColorName(color)}: нет камня за ширмой",
				Disabled = !bidEnabled,
				CustomMinimumSize = new Vector2(gemSize, gemSize),
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
				MouseDefaultCursorShape = bidEnabled ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow
			};
			gemBtn.AddThemeConstantOverride("icon_max_width", (int)(gemSize * 0.85f));
			if (bidEnabled)
			{
				var bidColor = color;
				gemBtn.Pressed += () => Bid(bidColor);
			}

			col.AddChild(gemBtn);
			col.AddChild(MakeAuctionThumb(slot.CardAtTip, "остр."));
			_auctionCards.AddChild(col);
		}
	}

	static string CardShortName(CardInstance card, CardDatabase? cards)
	{
		if (card.Kind == CardKind.Character && cards?.Characters.TryGetValue(card.DefinitionId, out var ch) == true)
			return ch.Name;
		if (card.Kind == CardKind.Law)
			return $"Закон #{card.DefinitionId}";
		return $"#{card.DefinitionId}";
	}

	void RebuildChoiceButtons(PendingCardChoice? pending, CardDatabase? cards)
	{
		ClearContainer(_choiceRow);

		if (pending is null)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		foreach (var id in pending.Options)
		{
			var tip = cards is null ? $"#{id}" : CardTooltips.ForCard(cards, CardKind.Character, id);
			var thumb = new CardThumb();
			_choiceRow.AddChild(thumb);
			var captured = id;
			var name = CardShortName(new CardInstance { InstanceId = 0, Kind = CardKind.Character, DefinitionId = id }, cards);
			thumb.SetupCard(
				cards,
				CardKind.Character,
				id,
				$"Взять\n{name}",
				tip,
				ThumbHand,
				() =>
				{
					session.Submit(new ChooseAuctionCardCommand(session.Session?.LocalPlayerId ?? 0, captured));
					Refresh();
				},
				showCaption: true);
			BindInspect(thumb, cards, CardKind.Character, id, name, tip);
		}
	}

	void RebuildPassGems(PendingPassGems? pending)
	{
		foreach (var child in _passGemsRow.GetChildren())
			child.QueueFree();

		if (pending is null)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;
		_passGemsRow.AddChild(new Label
		{
			Text = $"Камни за пас ({pending.Picked.Count}/{pending.Amount}):"
		});

		foreach (GemColor color in Enum.GetValues<GemColor>())
		{
			var c = color;
			var btn = MakeGemButton(c, disabled: pending.Picked.Count >= pending.Amount);
			btn.Pressed += () =>
			{
				session.Submit(new ClaimPassGemsCommand(pid, Color: c));
				Refresh();
			};
			_passGemsRow.AddChild(btn);
		}

		var confirm = new Button { Text = "Подтвердить" };
		confirm.Pressed += () =>
		{
			session.Submit(new ClaimPassGemsCommand(pid, Confirm: true));
			Refresh();
		};
		_passGemsRow.AddChild(confirm);
	}

	void RebuildRewardChoice(PendingRewardChoice? pending)
	{
		foreach (var child in _rewardRow.GetChildren())
			child.QueueFree();

		if (pending is null)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;
		_rewardRow.AddChild(new Label { Text = "Награда:" });

		for (var i = 0; i < pending.Options.Count; i++)
		{
			var idx = i;
			var label = i < pending.OptionLabels.Count
				? pending.OptionLabels[i]
				: $"Вариант {i + 1}";
			var btn = new Button { Text = label };
			btn.Pressed += () =>
			{
				session.Submit(new ChooseRewardCommand(pid, idx));
				Refresh();
			};
			_rewardRow.AddChild(btn);
		}
	}

	void RebuildDeckDrawPrompt(PendingDeckDraw? pending, GameState state)
	{
		if (pending is null)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;
		var lawLeft = state.LawDeck.Count;
		var smallLeft = state.SmallDeck.Count;
		var body = pending.Remaining > 1
			? $"Осталось взять: {pending.Remaining}"
			: "Возьмите 1 карту";

		var actions = new List<(string, Action, bool)>
		{
			($"Колода законов ({lawLeft})", () =>
			{
				session.Submit(new ChooseDeckDrawCommand(pid, FromLawDeck: true));
				Refresh();
			}, lawLeft == 0),
			($"Малая колода ({smallLeft})", () =>
			{
				session.Submit(new ChooseDeckDrawCommand(pid, FromLawDeck: false));
				Refresh();
			}, smallLeft == 0)
		};

		_actionModal.ShowChoices("Взять карту", body, actions);
	}

	void RebuildLawPrompt(GameState? state, PlayerState local, CardDatabase? cards)
	{
		foreach (var child in _lawPromptRow.GetChildren())
			child.QueueFree();

		if (state?.PendingLaw is not { } pending)
		{
			_actionModal.HideModal();
			return;
		}

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;
		var title = $"Закон #{pending.LawDefinitionId}";
		var body = cards?.Laws.TryGetValue(pending.LawDefinitionId, out var law) == true
			? CardTooltips.FormatLawText(law.Text)
			: null;

		switch (pending.Kind)
		{
			case LawPromptKind.ChooseOption:
			case LawPromptKind.OptionalSwapDecline:
			case LawPromptKind.ParkGems:
			case LawPromptKind.OfferTuckDrawnCard:
			{
				var actions = new List<(string, Action, bool)>();
				for (var i = 0; i < pending.OptionLabels.Count; i++)
				{
					var idx = i;
					actions.Add((pending.OptionLabels[i], () =>
					{
						session.Submit(new ResolveLawCommand(pid, OptionIndex: idx));
						Refresh();
					}, false));
				}

				_actionModal.ShowChoices(title, body, actions);
				break;
			}

			case LawPromptKind.ChooseInfiniteColor:
			{
				var gems = Enum.GetValues<GemColor>()
					.Select(c =>
					{
						var color = c;
						return (color, (Action)(() =>
						{
							session.Submit(new ResolveLawCommand(pid, GemColor: color));
							Refresh();
						}));
					})
					.ToList();
				_actionModal.ShowGemChoices(title, body ?? "Выберите цвет неисчерпаемого камня", gems);
				break;
			}

			case LawPromptKind.TakeAuctionCard:
			{
				var actions = new List<(string, Action, bool)>();
				foreach (var id in state.AuctionSlots.SelectMany(slot => slot.AvailableCards()))
				{
					var name = cards?.Characters.TryGetValue(id, out var ch) == true ? ch.Name : $"#{id}";
					var captured = id;
					actions.Add(($"Взять с аукциона: {name}", () =>
					{
						session.Submit(new ResolveLawCommand(pid, CharacterDefinitionId: captured));
						Refresh();
					}, false));
				}

				if (actions.Count == 0)
					actions.Add(("Нет доступных карт на аукционе", () => { }, true));
				_actionModal.ShowChoices(title, body, actions);
				break;
			}

			case LawPromptKind.ReturnLawsToDeck:
			{
				var hint = (body ?? "") + "\n\nВыберите ровно 2 закона из руки, затем подтвердите.";
				var actions = new List<(string, Action, bool)>();
				for (var i = 0; i < local.Hand.Count; i++)
				{
					if (local.Hand[i].Kind != CardKind.Law)
						continue;
					var idx = i;
					var selected = _lawReturnSelection.Contains(idx);
					actions.Add(($"{(selected ? "✓ " : "")}Закон #{local.Hand[i].DefinitionId}", () =>
					{
						if (!_lawReturnSelection.Add(idx))
							_lawReturnSelection.Remove(idx);
						Refresh();
					}, false));
				}

				actions.Add(($"Вернуть выбранные ({_lawReturnSelection.Count}/2)", () =>
				{
					session.Submit(new ResolveLawCommand(pid, HandIndices: _lawReturnSelection.ToList()));
					_lawReturnSelection.Clear();
					Refresh();
				}, _lawReturnSelection.Count != 2));
				_actionModal.ShowChoices(title, hint.Trim(), actions);
				break;
			}

			default:
				_actionModal.HideModal();
				break;
		}
	}

	void RebuildTokenSwap(GameState? state, PlayerState local, CardDatabase? cards, bool swapActive)
	{
		foreach (var child in _tokenSwapRow.GetChildren())
			child.QueueFree();

		if (state?.PendingTokenSwap is not { } pending)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var pid = session.Session?.LocalPlayerId ?? 0;

		_tokenSwapRow.AddChild(new Label { Text = "Обмен #67:" });

		var decline = new Button { Text = "Отказаться" };
		decline.Pressed += () =>
		{
			session.Submit(new ResolveTokenSwapCommand(pid, Decline: true));
			Refresh();
		};
		_tokenSwapRow.AddChild(decline);

		if (!swapActive)
			return;

		_tokenSwapRow.AddChild(new Label
		{
			Text = $"своё:{(pending.OwnCardInstanceId?.ToString() ?? "-")}/" +
			       $"{TokenRu(pending.OwnToken)} | чужое:p{pending.OtherPlayerId?.ToString() ?? "-"}/" +
			       $"{pending.OtherCardInstanceId?.ToString() ?? "-"}/{TokenRu(pending.OtherToken)} | " +
			       $"оплата {pending.Payment.Count}/3"
		});

		foreach (var pc in local.Pyramid.AllCards)
		{
			foreach (var (kind, amount) in TokensOn(pc))
			{
				if (amount <= 0)
					continue;
				var inst = pc.Card.InstanceId;
				var tok = kind;
				var name = CardShortName(pc.Card, cards);
				var btn = new Button { Text = $"Своё {name}:{TokenRu(kind)}×{amount}" };
				btn.Pressed += () =>
				{
					session.Submit(new ResolveTokenSwapCommand(pid, OwnCardInstanceId: inst, OwnToken: tok));
					Refresh();
				};
				_tokenSwapRow.AddChild(btn);
			}
		}

		foreach (var other in state.Players.Where(p => p.PlayerId != local.PlayerId && p.Pyramid.AllCards.Any()))
		{
			foreach (var pc in other.Pyramid.AllCards)
			{
				foreach (var (kind, amount) in TokensOn(pc))
				{
					if (amount <= 0)
						continue;
					var op = other.PlayerId;
					var inst = pc.Card.InstanceId;
					var tok = kind;
					var name = CardShortName(pc.Card, cards);
					var btn = new Button { Text = $"{other.DisplayName}:{name}:{TokenRu(kind)}×{amount}" };
					btn.Pressed += () =>
					{
						session.Submit(new ResolveTokenSwapCommand(
							pid,
							OtherPlayerId: op,
							OtherCardInstanceId: inst,
							OtherToken: tok));
						Refresh();
					};
					_tokenSwapRow.AddChild(btn);
				}
			}
		}

		foreach (GemColor color in Enum.GetValues<GemColor>())
		{
			var c = color;
			var btn = MakeGemButton(c, disabled: pending.Payment.Count >= 3 || local.Screen[color] <= 0);
			btn.TooltipText = $"Оплата: {GemIcons.ColorName(c)}";
			btn.Pressed += () =>
			{
				session.Submit(new ResolveTokenSwapCommand(pid, PayGem: c));
				Refresh();
			};
			_tokenSwapRow.AddChild(btn);
		}

		var ready = pending.OwnCardInstanceId is not null
			&& pending.OwnToken is not null
			&& pending.OtherPlayerId is not null
			&& pending.OtherCardInstanceId is not null
			&& pending.OtherToken is not null
			&& pending.Payment.Count == 3;
		var confirm = new Button { Text = "Подтвердить обмен", Disabled = !ready };
		confirm.Pressed += () =>
		{
			session.Submit(new ResolveTokenSwapCommand(pid, Confirm: true));
			Refresh();
		};
		_tokenSwapRow.AddChild(confirm);
	}

	static IEnumerable<(TokenKind Kind, int Amount)> TokensOn(PyramidCard pc)
	{
		yield return (TokenKind.VictoryPoints, pc.VictoryPoints);
		yield return (TokenKind.Science, pc.Science);
		yield return (TokenKind.Magic, pc.Magic);
		yield return (TokenKind.Defense, pc.Defense);
	}

	static string TokenRu(TokenKind? kind) => kind switch
	{
		TokenKind.VictoryPoints => "VP",
		TokenKind.Science => "Наука",
		TokenKind.Magic => "Магия",
		TokenKind.Defense => "Защита",
		_ => "-"
	};

	static Button MakeGemButton(GemColor color, bool disabled = false)
	{
		var btn = new Button
		{
			Icon = GemIcons.Get(color),
			Text = "",
			Disabled = disabled,
			TooltipText = GemIcons.ColorName(color),
			CustomMinimumSize = new Vector2(44, 40),
			ExpandIcon = true
		};
		btn.AddThemeConstantOverride("icon_max_width", 28);
		return btn;
	}

	void RebuildHand(PlayerState? player, CardDatabase? cards, bool canDev)
	{
		ClearContainer(_handBox);

		if (player is null)
			return;

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		var legal = player.Pyramid.LegalPlacements();
		var pid = session.Session?.LocalPlayerId ?? 0;

		if (_buildMode && _selectedHandIndex is int sel && (sel < 0 || sel >= player.Hand.Count))
			ClearBuildDraft();

		for (var i = 0; i < player.Hand.Count; i++)
		{
			var card = player.Hand[i];
			var title = CardShortName(card, cards);
			var selected = _buildMode && _selectedHandIndex == i;
			var tip = cards is null
				? title
				: CardTooltips.ForCard(cards, card.Kind, card.DefinitionId);

			var col = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
			};
			col.AddThemeConstantOverride("separation", 4);
			var thumb = new CardThumb();
			col.AddChild(thumb);
			var hi = i;
			thumb.SetupCard(
				cards,
				card.Kind,
				card.DefinitionId,
				title,
				tip + (canDev ? "\n\nПеретащите на слот пирамиды" : ""),
				ThumbHand,
				canDev && !_buildMode
					? () => SelectHandCard(hi)
					: null);
			thumb.SetSelected(selected);
			BindInspect(thumb, cards, card.Kind, card.DefinitionId, title, tip);
			if (canDev)
				thumb.EnableHandDrag(hi, card.Kind, card.DefinitionId);

			if (canDev)
			{
				var actions = new HBoxContainer();
				var discardBtn = new Button { Text = "Сброс+2", Disabled = _buildMode && !selected };
				discardBtn.Pressed += () =>
				{
					ClearBuildDraft();
					session.Submit(new DiscardForGemsCommand(pid, hi));
					Refresh();
				};
				actions.AddChild(discardBtn);

				if (!_buildMode)
				{
					var selectBtn = new Button { Text = "Выбрать" };
					selectBtn.Pressed += () => SelectHandCard(hi);
					actions.AddChild(selectBtn);
				}

				col.AddChild(actions);
			}

			if (canDev && _buildMode && _selectedHandIndex is int lawIdx
			    && player.Hand[lawIdx].Kind == CardKind.Law
			    && player.Hand[lawIdx].DefinitionId == LawIds.TuckFromHand
			    && i != lawIdx)
			{
				var tuckBtn = new Button
				{
					Text = _law72TuckSelection.Contains(i) ? "✓ подложить" : "подложить?"
				};
				var tuckIdx = i;
				tuckBtn.Pressed += () =>
				{
					if (!_law72TuckSelection.Add(tuckIdx))
						_law72TuckSelection.Remove(tuckIdx);
					Refresh();
				};
				col.AddChild(tuckBtn);
			}

			if (canDev && selected)
				AddPlacementActions(col, player, card, i, legal, cards, session, pid);

			_handBox.AddChild(col);
		}
	}

	void AddPlacementActions(
		VBoxContainer col,
		PlayerState player,
		CardInstance card,
		int handIndex,
		List<(int Level, int Index)> legal,
		CardDatabase? cards,
		GameSessionAutoload session,
		int pid)
	{
		var wrap = new HFlowContainer();
		col.AddChild(wrap);

		if (card.Kind == CardKind.Law && LawIds.NeedsTargetOnPlay(card.DefinitionId)
		    && card.DefinitionId is LawIds.Replace or LawIds.TuckUnderCharacter)
		{
			foreach (var target in player.Pyramid.AllCards)
			{
				if (card.DefinitionId == LawIds.TuckUnderCharacter && target.Card.Kind != CardKind.Character)
					continue;
				var tName = CardShortName(target.Card, cards);
				var tid = target.Card.InstanceId;
				var btn = new Button { Text = card.DefinitionId == LawIds.Replace ? $"↔{tName}" : $"↓{tName}" };
				btn.Pressed += () =>
				{
					session.Submit(new PlayCardCommand(pid, handIndex, 1, 0, LawTargetInstanceId: tid));
					ClearBuildDraft();
					Refresh();
				};
				wrap.AddChild(btn);
			}

			return;
		}

		foreach (var (level, index) in legal)
		{
			if (card.Kind == CardKind.Law && level == 5)
				continue;

			var canAfford = true;
			string costTip = "";
			if (card.Kind == CardKind.Character && cards is not null)
			{
				var def = cards.GetCharacter(card.DefinitionId);
				var baseCost = PlayCostHelper.BaseCost(def, level);
				var effective = PlayCostHelper.EffectiveCost(player, def, level);
				canAfford = PlayCostHelper.CanAfford(player, def, level);
				var baseDesc = string.Join(" ", baseCost.Where(kv => kv.Value > 0)
					.Select(kv => $"{CardTooltips.ColorRu(kv.Key)}×{kv.Value}"));
				var payDesc = string.Join(" ", effective.Where(kv => kv.Value > 0)
					.Select(kv => $"{CardTooltips.ColorRu(kv.Key)}×{kv.Value}"));
				costTip = string.IsNullOrEmpty(payDesc)
					? $"База: {baseDesc} (полностью ∞)"
					: $"К оплате: {payDesc} (база {baseDesc})";
			}

			if (card.Kind == CardKind.Law && card.DefinitionId == LawIds.TuckFreeCard)
			{
				foreach (var free in player.Pyramid.FreeCards())
				{
					var fName = CardShortName(free.Card, cards);
					var playBtn = new Button
					{
						Text = $"L{level}+↓{fName}",
						Disabled = !canAfford,
						TooltipText = string.IsNullOrEmpty(costTip) ? "Свободная карта" : $"Оплата: {costTip}"
					};
					var lvl = level;
					var idx = index;
					var fid = free.Card.InstanceId;
					playBtn.Pressed += () =>
					{
						session.Submit(new PlayCardCommand(pid, handIndex, lvl, idx, LawTargetInstanceId: fid));
						ClearBuildDraft();
						Refresh();
					};
					wrap.AddChild(playBtn);
				}

				continue;
			}

			var placeBtn = new Button
			{
				Text = $"→L{level}:{(level == 1 ? (index == 0 ? "лево" : "право") : $"оп{index}")}",
				Disabled = !canAfford,
				TooltipText = string.IsNullOrEmpty(costTip) ? "Размещение" : $"Оплата: {costTip}"
			};
			var pl = level;
			var pi = index;
			placeBtn.Pressed += () =>
			{
				IReadOnlyList<int>? extras = null;
				if (card.Kind == CardKind.Law && card.DefinitionId == LawIds.TuckFromHand)
					extras = _law72TuckSelection.Where(x => x != handIndex).ToList();

				session.Submit(new PlayCardCommand(pid, handIndex, pl, pi, ExtraHandIndices: extras));
				ClearBuildDraft();
				Refresh();
			};
			wrap.AddChild(placeBtn);
		}
	}

	static string ColorRu(GemColor c) => c switch
	{
		GemColor.Blue => "Син",
		GemColor.Red => "Крас",
		GemColor.Green => "Зел",
		GemColor.Yellow => "Жёл",
		_ => c.ToString()
	};

	void AppendLog(string message)
	{
		_log.AppendText(message + "\n");
	}

	void Bid(GemColor color)
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.Submit(new BidGemCommand(session.Session?.LocalPlayerId ?? 0, color));
		Refresh();
	}

	void OnPassAuction()
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.Submit(new PassAuctionCommand(session.Session?.LocalPlayerId ?? 0));
		Refresh();
	}

	void OnAttack()
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.Submit(new BidAttackCommand(session.Session?.LocalPlayerId ?? 0));
		Refresh();
	}

	void OnPassDev()
	{
		ClearBuildDraft();
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.Submit(new PassDevelopmentCommand(session.Session?.LocalPlayerId ?? 0));
		Refresh();
	}

	void OnUndo()
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		ClearBuildDraft();
		session.Submit(new UndoCommand(session.Session?.LocalPlayerId ?? 0));
		Refresh();
	}

	void OnCancelBuild()
	{
		ClearBuildDraft();
		Refresh();
	}

	void ChooseLevel5(bool fifteen)
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.Submit(new ChooseLevel5RewardCommand(session.Session?.LocalPlayerId ?? 0, fifteen));
		Refresh();
	}

	void OnMenu()
	{
		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.ShutdownSession();
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}
}
