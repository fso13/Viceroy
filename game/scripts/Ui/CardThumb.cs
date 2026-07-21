using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Card thumbnail: click, long-press inspect, and/or drag from hand.</summary>
public partial class CardThumb : PanelContainer
{
	public TextureRect Art { get; private set; } = null!;
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

	const float LongPressSeconds = 0.45f;
	const float MoveSlop = 10f;

	static readonly StyleBoxFlat NormalStyle = MakeStyle(new Color(0.18f, 0.16f, 0.12f), new Color(0.45f, 0.38f, 0.28f));
	static readonly StyleBoxFlat SelectedStyle = MakeStyle(new Color(0.28f, 0.24f, 0.14f), new Color(0.85f, 0.72f, 0.35f));
	static readonly StyleBoxFlat DropHotStyle = MakeStyle(new Color(0.22f, 0.28f, 0.18f), new Color(0.7f, 0.9f, 0.4f));

	public override void _Ready()
	{
		if (Art is not null)
			return;
		Build();
	}

	HFlowContainer _badges = null!;

	void Build()
	{
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", NormalStyle);

		var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		vbox.AddThemeConstantOverride("separation", 2);

		var artStack = new Control { MouseFilter = MouseFilterEnum.Ignore };
		Art = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(96, 96),
			MouseFilter = MouseFilterEnum.Ignore
		};
		Art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_badges = new HFlowContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Alignment = FlowContainer.AlignmentMode.Center
		};
		_badges.AddThemeConstantOverride("h_separation", 2);
		_badges.AddThemeConstantOverride("v_separation", 1);
		_badges.SetAnchorsPreset(LayoutPreset.BottomWide);
		_badges.OffsetTop = -22;
		_badges.OffsetBottom = -2;
		_badges.OffsetLeft = 2;
		_badges.OffsetRight = -2;

		artStack.AddChild(Art);
		artStack.AddChild(_badges);

		Caption = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore
		};
		Caption.AddThemeFontSizeOverride("font_size", 10);

		vbox.AddChild(artStack);
		vbox.AddChild(Caption);
		AddChild(vbox);
	}

	public void Setup(
		Texture2D texture,
		string caption,
		string tooltip,
		Vector2 artSize,
		Action? onPressed = null,
		bool showCaption = true)
	{
		if (Art is null)
			Build();

		Art!.Texture = texture;
		Art.GetParent<Control>().CustomMinimumSize = artSize;
		Caption.Text = caption;
		Caption.Visible = showCaption && !string.IsNullOrEmpty(caption);
		TooltipText = tooltip + "\n(удерживайте для увеличения)";
		var captionH = Caption.Visible ? 18f : 0f;
		CustomMinimumSize = new Vector2(artSize.X + 4, artSize.Y + 4 + captionH);
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

		// Cancel long-press path once drag starts.
		_pressing = false;
		SetProcess(false);

		var preview = new TextureRect
		{
			Texture = Art.Texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(72, 72),
			Modulate = new Color(1, 1, 1, 0.85f)
		};
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

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
			AddThemeStyleboxOverride("panel", Selected ? SelectedStyle : NormalStyle);
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
