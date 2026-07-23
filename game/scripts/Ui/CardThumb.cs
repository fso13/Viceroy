using Godot;
using Namestnik.Core;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Card thumbnail: click, long-press inspect, and/or drag from hand.</summary>
public partial class CardThumb : PanelContainer
{
	public CardFaceView Face { get; private set; } = null!;
	public Label Caption { get; private set; } = null!;

	public bool Selected { get; private set; }

	Action? _onPressed;
	Action? _onInspect;
	Func<int, bool>? _canAcceptDrop;
	Action<int>? _onCardDrop;
	int? _dragHandIndex;
	CardKind _dragKind;
	int _dragDefinitionId;

	Vector2 _pressLocal;
	Vector2 _pressGlobal;
	bool _pressing;
	bool _longFired;
	double _pressSeconds;

	Control _artStack = null!;
	HFlowContainer _badges = null!;

	const float LongPressSeconds = 0.45f;
	const float MoveSlop = 10f;
	const float CaptionHeight = 18f;
	const float Chrome = 6f; // panel margins + vbox gap

	static readonly StyleBoxFlat NormalStyle = MakeStyle(new Color(0.18f, 0.16f, 0.12f), new Color(0.45f, 0.38f, 0.28f));
	static readonly StyleBoxFlat SelectedStyle = MakeStyle(new Color(0.28f, 0.24f, 0.14f), new Color(0.85f, 0.72f, 0.35f));
	static readonly StyleBoxFlat DropHotStyle = MakeStyle(new Color(0.22f, 0.28f, 0.18f), new Color(0.7f, 0.9f, 0.4f));

	public override void _Ready()
	{
		if (Face is not null)
			return;
		Build();
	}

	void Build()
	{
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
		SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		SizeFlagsVertical = SizeFlags.ShrinkBegin;
		AddThemeStyleboxOverride("panel", NormalStyle);

		var vbox = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.Fill,
			SizeFlagsVertical = SizeFlags.Fill
		};
		vbox.AddThemeConstantOverride("separation", 2);

		_artStack = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = true,
			CustomMinimumSize = new Vector2(96, 96)
		};
		Face = new CardFaceView
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		Face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_badges = new HFlowContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = FlowContainer.AlignmentMode.Center
		};
		_badges.AddThemeConstantOverride("h_separation", 2);
		_badges.AddThemeConstantOverride("v_separation", 1);
		_badges.SetAnchorsPreset(LayoutPreset.TopWide);
		_badges.OffsetTop = 2;
		_badges.OffsetBottom = 22;
		_badges.OffsetLeft = 2;
		_badges.OffsetRight = -2;

		_artStack.AddChild(Face);
		_artStack.AddChild(_badges);

		Caption = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = MouseFilterEnum.Ignore,
			ClipText = true,
			CustomMinimumSize = new Vector2(0, CaptionHeight)
		};
		Caption.AddThemeFontSizeOverride("font_size", 11);

		vbox.AddChild(_artStack);
		vbox.AddChild(Caption);
		AddChild(vbox);
	}

	/// <summary>Outer size of a thumb with the given art size (caption always reserved for alignment).</summary>
	public static Vector2 OuterSize(Vector2 artSize, bool reserveCaption = true) =>
		new(artSize.X + Chrome, artSize.Y + Chrome + (reserveCaption ? CaptionHeight : 0f));

	public void SetupBack(
		string caption,
		string tooltip,
		Vector2 artSize,
		bool showCaption = false,
		bool reserveCaption = true)
	{
		if (Face is null)
			Build();
		Face!.ShowBack();
		ApplyChrome(caption, tooltip, artSize, showCaption, reserveCaption);
	}

	public void SetupCard(
		CardDatabase? db,
		CardKind kind,
		int definitionId,
		string caption,
		string tooltip,
		Vector2 artSize,
		Action? onPressed = null,
		bool showCaption = false,
		bool reserveCaption = true,
		IReadOnlyDictionary<string, GemColor>? sectorOverrides = null)
	{
		if (Face is null)
			Build();
		Face!.ShowFromDb(db, kind, definitionId, sectorOverrides);
		ApplyChrome(caption, tooltip, artSize, showCaption, reserveCaption, onPressed);
		ClearSectorHotspots();
	}

	/// <summary>End-game recolor: clickable corner pads on the card art.</summary>
	public void EnableSectorRecolor(Action<string> onPaint, Action<string> onClear)
	{
		if (_artStack is null)
			Build();
		ClearSectorHotspots();
		_artStack!.MouseFilter = MouseFilterEnum.Stop;

		AddCornerHotspot("tl", new Vector2(0.02f, 0.02f), onPaint, onClear);
		AddCornerHotspot("tr", new Vector2(0.72f, 0.02f), onPaint, onClear);
		AddCornerHotspot("bl", new Vector2(0.28f, 0.72f), onPaint, onClear);
		AddCornerHotspot("br", new Vector2(0.52f, 0.72f), onPaint, onClear);
	}

	void AddCornerHotspot(string corner, Vector2 anchor, Action<string> onPaint, Action<string> onClear)
	{
		var btn = new Button
		{
			Text = "",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = corner switch
			{
				"tl" => "Верхний левый сектор\nЛКМ — окрасить выбранным цветом\nПКМ — снять окраску",
				"tr" => "Верхний правый сектор\nЛКМ — окрасить выбранным цветом\nПКМ — снять окраску",
				"bl" => "Нижний левый сектор\nЛКМ — окрасить выбранным цветом\nПКМ — снять окраску",
				_ => "Нижний правый сектор\nЛКМ — окрасить выбранным цветом\nПКМ — снять окраску"
			}
		};
		btn.SetAnchorsPreset(LayoutPreset.TopLeft);
		btn.AnchorLeft = anchor.X;
		btn.AnchorTop = anchor.Y;
		btn.AnchorRight = anchor.X + 0.26f;
		btn.AnchorBottom = anchor.Y + 0.26f;
		btn.OffsetLeft = 0;
		btn.OffsetTop = 0;
		btn.OffsetRight = 0;
		btn.OffsetBottom = 0;
		btn.GuiInput += e =>
		{
			if (e is not InputEventMouseButton { Pressed: true } mb)
				return;
			if (mb.ButtonIndex == MouseButton.Left)
			{
				onPaint(corner);
				GetViewport().SetInputAsHandled();
			}
			else if (mb.ButtonIndex == MouseButton.Right)
			{
				onClear(corner);
				GetViewport().SetInputAsHandled();
			}
		};
		btn.SetMeta("sector_hotspot", true);
		_artStack.AddChild(btn);
	}

	void ClearSectorHotspots()
	{
		if (_artStack is null)
			return;
		foreach (var child in _artStack.GetChildren().ToArray())
		{
			if (child is Control c && c.HasMeta("sector_hotspot"))
				c.QueueFree();
		}
	}


	void ApplyChrome(
		string caption,
		string tooltip,
		Vector2 artSize,
		bool showCaption,
		bool reserveCaption = true,
		Action? onPressed = null)
	{
		_artStack.CustomMinimumSize = artSize;
		Caption.Text = caption;
		Caption.Visible = reserveCaption;
		Caption.Modulate = showCaption && !string.IsNullOrEmpty(caption)
			? Colors.White
			: new Color(1, 1, 1, 0);
		TooltipText = tooltip + "\n(удерживайте для увеличения)";

		var outer = OuterSize(artSize, reserveCaption);
		CustomMinimumSize = outer;
		Size = outer;

		ClearBadges();
		_onPressed = onPressed;
		_dragHandIndex = null;
		_canAcceptDrop = null;
		_onCardDrop = null;
		_onInspect = null;
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public void SetTokenBadges(IReadOnlyList<(string Text, Color Color)> badges)
	{
		if (_badges is null)
			return;
		ClearBadges();
		foreach (var (text, color) in badges)
		{
			var chip = new Label
			{
				Text = text,
				MouseFilter = MouseFilterEnum.Ignore,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			chip.AddThemeFontSizeOverride("font_size", 9);
			chip.AddThemeColorOverride("font_color", color);
			chip.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
			chip.AddThemeConstantOverride("outline_size", 3);
			_badges.AddChild(chip);
		}
	}

	void ClearBadges()
	{
		if (_badges is null)
			return;
		foreach (var child in _badges.GetChildren())
			child.QueueFree();
	}

	public void EnableInspect(Action onInspect) => _onInspect = onInspect;

	public void EnableHandDrag(int handIndex, CardKind kind, int definitionId)
	{
		_dragHandIndex = handIndex;
		_dragKind = kind;
		_dragDefinitionId = definitionId;
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public void EnableCardDrop(Func<int, bool> canAccept, Action<int> onDrop)
	{
		_canAcceptDrop = canAccept;
		_onCardDrop = onDrop;
	}

	public void SetSelected(bool selected)
	{
		Selected = selected;
		AddThemeStyleboxOverride("panel", selected ? SelectedStyle : NormalStyle);
		if (selected && IsInsideTree())
			UiAnim.PulseOnce(this, peak: 1.08f, duration: 0.2f);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
			AddThemeStyleboxOverride("panel", Selected ? SelectedStyle : NormalStyle);
		else if (what == NotificationMouseEnter && (_dragHandIndex is not null || _onPressed is not null))
			UiAnim.HoverScale(this, hovered: true, hover: 1.06f);
		else if (what == NotificationMouseExit)
			UiAnim.HoverScale(this, hovered: false);
	}

	public override void _Process(double delta)
	{
		if (!_pressing || _longFired || _onInspect is null)
			return;

		if (GetGlobalMousePosition().DistanceTo(_pressGlobal) > MoveSlop)
			return;

		_pressSeconds += delta;
		if (_pressSeconds < LongPressSeconds)
			return;

		_longFired = true;
		_onInspect.Invoke();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
		{
			if (mb.Pressed)
			{
				_pressing = true;
				_longFired = false;
				_pressSeconds = 0;
				_pressLocal = mb.Position;
				_pressGlobal = GetGlobalMousePosition();
				SetProcess(true);
			}
			else if (_pressing)
			{
				_pressing = false;
				SetProcess(false);
				if (!_longFired
				    && _onPressed is not null
				    && _pressLocal.DistanceTo(mb.Position) <= MoveSlop)
				{
					_onPressed.Invoke();
					AcceptEvent();
				}
			}
		}
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (_dragHandIndex is not int handIndex || _longFired)
			return default;

		_pressing = false;
		SetProcess(false);

		var preview = new CardFaceView
		{
			CustomMinimumSize = new Vector2(72, 72),
			Size = new Vector2(72, 72),
			Modulate = new Color(1, 1, 1, 0.85f)
		};
		preview.CopyContentFrom(Face);
		SetDragPreview(preview);
		return PyramidDropSlot.MakeDragPayload(handIndex, _dragKind, _dragDefinitionId);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (_canAcceptDrop is null || !PyramidDropSlot.TryReadHandIndex(data, out var handIndex))
		{
			if (!Selected)
				AddThemeStyleboxOverride("panel", NormalStyle);
			return false;
		}

		var ok = _canAcceptDrop(handIndex);
		AddThemeStyleboxOverride("panel", ok ? DropHotStyle : NormalStyle);
		return ok;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		AddThemeStyleboxOverride("panel", Selected ? SelectedStyle : NormalStyle);
		if (!PyramidDropSlot.TryReadHandIndex(data, out var handIndex))
			return;
		_onCardDrop?.Invoke(handIndex);
	}

	static StyleBoxFlat MakeStyle(Color bg, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(2);
		return style;
	}
}
