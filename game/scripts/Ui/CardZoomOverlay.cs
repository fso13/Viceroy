using Godot;

namespace Namestnik.Ui;

/// <summary>Fullscreen (modal) enlarged card preview.</summary>
public partial class CardZoomOverlay : Control
{
	ColorRect _dim = null!;
	PanelContainer _panel = null!;
	TextureRect _art = null!;
	Label _title = null!;
	Label _body = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;
		ZIndex = 100;

		_dim = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.72f),
			MouseFilter = MouseFilterEnum.Stop
		};
		_dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_dim.GuiInput += OnDimInput;
		AddChild(_dim);

		_panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Stop
		};
		_panel.SetAnchorsPreset(LayoutPreset.Center);
		_panel.GrowHorizontal = GrowDirection.Both;
		_panel.GrowVertical = GrowDirection.Both;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.11f, 0.09f, 0.98f),
			BorderColor = new Color(0.75f, 0.65f, 0.4f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(10);
		style.SetContentMarginAll(12);
		_panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		_title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_title.AddThemeFontSizeOverride("font_size", 18);
		_art = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(420, 420),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		_body = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(420, 0)
		};
		_body.AddThemeFontSizeOverride("font_size", 13);

		var close = new Button { Text = "Закрыть" };
		close.Pressed += HideZoom;

		vbox.AddChild(_title);
		vbox.AddChild(_art);
		vbox.AddChild(_body);
		vbox.AddChild(close);
		_panel.AddChild(vbox);
		AddChild(_panel);
	}

	public void ShowCard(Texture2D texture, string title, string details)
	{
		if (!IsNodeReady() || _art is null)
			_Ready();
		_art!.Texture = texture;
		_title.Text = title;
		_body.Text = details;
		Visible = true;
		CallDeferred(nameof(CenterPanel));
	}

	void CenterPanel()
	{
		var size = _panel.GetCombinedMinimumSize();
		_panel.Position = (Size - size) * 0.5f;
	}

	public void HideZoom() => Visible = false;

	void OnDimInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			HideZoom();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible)
			return;
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
		{
			HideZoom();
			GetViewport().SetInputAsHandled();
		}
	}
}
