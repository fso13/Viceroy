using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Empty pyramid cell that accepts a dragged hand card.</summary>
public partial class PyramidDropSlot : PanelContainer
{
	public int Level { get; private set; }
	public int Index { get; private set; }

	Func<int, bool>? _canAccept;
	Action<int>? _onDrop;

	static readonly StyleBoxFlat Idle = MakeStyle(new Color(0.14f, 0.18f, 0.16f, 0.55f), new Color(0.45f, 0.7f, 0.55f, 0.7f));
	static readonly StyleBoxFlat Hot = MakeStyle(new Color(0.22f, 0.35f, 0.28f, 0.85f), new Color(0.55f, 0.95f, 0.65f, 1f));
	static readonly StyleBoxFlat Reject = MakeStyle(new Color(0.25f, 0.12f, 0.12f, 0.5f), new Color(0.7f, 0.3f, 0.3f, 0.6f));

	Label _label = null!;

	public void Configure(
		int level,
		int index,
		string caption,
		Vector2 size,
		string tooltip,
		Func<int, bool> canAccept,
		Action<int> onDrop)
	{
		Level = level;
		Index = index;
		_canAccept = canAccept;
		_onDrop = onDrop;

		if (_label is null)
			Build(size);

		_label!.Text = caption;
		TooltipText = tooltip;
		CustomMinimumSize = size;
		AddThemeStyleboxOverride("panel", Idle);
		MouseFilter = MouseFilterEnum.Stop;
	}

	void Build(Vector2 size)
	{
		CustomMinimumSize = size;
		SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		SizeFlagsVertical = SizeFlags.ShrinkBegin;
		AddThemeStyleboxOverride("panel", Idle);
		_label = new Label
		{
			Text = "+",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		_label.AddThemeFontSizeOverride("font_size", 14);
		_label.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 0.75f, 0.9f));
		AddChild(_label);
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (!TryReadHandIndex(data, out var handIndex) || _canAccept is null)
		{
			AddThemeStyleboxOverride("panel", Idle);
			return false;
		}

		var ok = _canAccept(handIndex);
		AddThemeStyleboxOverride("panel", ok ? Hot : Reject);
		return ok;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		AddThemeStyleboxOverride("panel", Idle);
		if (!TryReadHandIndex(data, out var handIndex))
			return;
		_onDrop?.Invoke(handIndex);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
			AddThemeStyleboxOverride("panel", Idle);
	}

	public static bool TryReadHandIndex(Variant data, out int handIndex)
	{
		handIndex = -1;
		if (data.VariantType != Variant.Type.Dictionary)
			return false;
		var dict = data.AsGodotDictionary();
		if (!dict.ContainsKey("hand_index"))
			return false;
		handIndex = dict["hand_index"].AsInt32();
		return handIndex >= 0;
	}

	public static Godot.Collections.Dictionary MakeDragPayload(int handIndex, CardKind kind, int definitionId) =>
		new()
		{
			["hand_index"] = handIndex,
			["kind"] = (int)kind,
			["definition_id"] = definitionId
		};

	static StyleBoxFlat MakeStyle(Color bg, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 4,
			ContentMarginTop = 4,
			ContentMarginRight = 4,
			ContentMarginBottom = 4
		};
		style.DrawCenter = true;
		return style;
	}
}
