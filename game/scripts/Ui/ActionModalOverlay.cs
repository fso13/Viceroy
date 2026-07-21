using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Blocking modal with title, optional body, and action buttons (laws, rewards, etc.).</summary>
public partial class ActionModalOverlay : Control
{
	ColorRect _dim = null!;
	PanelContainer _panel = null!;
	Label _title = null!;
	Label _body = null!;
	VBoxContainer _actions = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;
		ZIndex = 105;

		_dim = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.72f),
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
			BgColor = new Color(0.13f, 0.12f, 0.1f, 0.98f),
			BorderColor = new Color(0.75f, 0.62f, 0.35f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(10);
		style.SetContentMarginAll(18);
		_panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		vbox.CustomMinimumSize = new Vector2(480, 0);

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
			HorizontalAlignment = HorizontalAlignment.Left,
			Visible = false
		};
		_body.AddThemeFontSizeOverride("font_size", 14);
		_body.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.75f));

		_actions = new VBoxContainer();
		_actions.AddThemeConstantOverride("separation", 8);

		vbox.AddChild(_title);
		vbox.AddChild(_body);
		vbox.AddChild(_actions);
		_panel.AddChild(vbox);
		AddChild(_panel);
	}

	public void ShowChoices(string title, string? body, IReadOnlyList<(string Label, Action OnPressed, bool Disabled)> actions)
	{
		if (!IsNodeReady() || _title is null)
			_Ready();

		_title!.Text = title;
		if (string.IsNullOrWhiteSpace(body))
		{
			_body!.Visible = false;
			_body.Text = "";
		}
		else
		{
			_body!.Visible = true;
			_body.Text = body;
		}

		foreach (var child in _actions!.GetChildren())
			child.QueueFree();

		foreach (var (label, onPressed, disabled) in actions)
		{
			var btn = new Button
			{
				Text = label,
				Disabled = disabled,
				CustomMinimumSize = new Vector2(0, 40),
				SizeFlagsHorizontal = SizeFlags.Fill
			};
			btn.Pressed += onPressed;
			_actions.AddChild(btn);
		}

		Visible = true;
		MoveToFront();
		CallDeferred(nameof(CenterPanel));
	}

	public void ShowGemChoices(string title, string? body, IReadOnlyList<(GemColor Color, Action OnPressed)> gems)
	{
		if (!IsNodeReady() || _title is null)
			_Ready();

		_title!.Text = title;
		_body!.Visible = !string.IsNullOrWhiteSpace(body);
		_body.Text = body ?? "";

		foreach (var child in _actions!.GetChildren())
			child.QueueFree();

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		foreach (var (color, onPressed) in gems)
		{
			var btn = new Button
			{
				Icon = GemIcons.Get(color),
				ExpandIcon = true,
				Text = GemIcons.ColorName(color),
				CustomMinimumSize = new Vector2(110, 48),
				TooltipText = GemIcons.ColorName(color)
			};
			btn.AddThemeConstantOverride("icon_max_width", 28);
			btn.Pressed += onPressed;
			row.AddChild(btn);
		}

		_actions.AddChild(row);
		Visible = true;
		MoveToFront();
		CallDeferred(nameof(CenterPanel));
	}

	public void HideModal()
	{
		Visible = false;
		if (_actions is null)
			return;
		foreach (var child in _actions.GetChildren())
			child.QueueFree();
	}

	void CenterPanel()
	{
		if (_panel is null)
			return;
		var size = _panel.GetCombinedMinimumSize();
		_panel.Position = (Size - size) * 0.5f;
	}
}
