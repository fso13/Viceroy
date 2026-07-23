using Godot;

namespace Namestnik.Ui;

/// <summary>Blocking modal notice (auction conflicts, etc.).</summary>
public partial class NoticeOverlay : Control
{
	ColorRect _dim = null!;
	PanelContainer _panel = null!;
	Label _title = null!;
	Label _body = null!;
	readonly Queue<(string Title, string Body)> _queue = new();

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;
		ZIndex = 110;

		_dim = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.75f),
			MouseFilter = MouseFilterEnum.Stop
		};
		_dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_dim);

		_panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		_panel.SetAnchorsPreset(LayoutPreset.Center);
		_panel.GrowHorizontal = GrowDirection.Both;
		_panel.GrowVertical = GrowDirection.Both;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.14f, 0.13f, 0.11f, 0.98f),
			BorderColor = new Color(0.85f, 0.7f, 0.35f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(10);
		style.SetContentMarginAll(18);
		_panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		vbox.CustomMinimumSize = new Vector2(460, 0);

		_title = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_title.AddThemeFontSizeOverride("font_size", 20);
		_title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));

		_body = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_body.AddThemeFontSizeOverride("font_size", 15);

		var ok = new Button
		{
			Text = "Понятно",
			CustomMinimumSize = new Vector2(0, 40)
		};
		ok.Pressed += Dismiss;

		vbox.AddChild(_title);
		vbox.AddChild(_body);
		vbox.AddChild(ok);
		_panel.AddChild(vbox);
		AddChild(_panel);
	}

	public void Enqueue(string title, string body)
	{
		if (!IsNodeReady() || _title is null)
			_Ready();

		_queue.Enqueue((title, body));
		if (!Visible)
			ShowNext();
	}

	void ShowNext()
	{
		if (_queue.Count == 0)
		{
			Visible = false;
			return;
		}

		var (title, body) = _queue.Dequeue();
		_title.Text = title;
		_body.Text = body;
		var wasHidden = !Visible;
		Visible = true;
		MoveToFront();
		if (wasHidden)
			CallDeferred(nameof(PlayEnter));
	}

	void PlayEnter()
	{
		if (!Visible || _panel is null)
			return;
		UiAnim.OverlayIn(_dim, _panel);
	}

	void Dismiss() => ShowNext();
}
