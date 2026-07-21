using Godot;
using Namestnik.Net;

namespace Namestnik.Ui;

public partial class MainMenuController : Control
{
	LineEdit _addressEdit = null!;
	Label _statusLabel = null!;
	OptionButton _resolutionOption = null!;
	CheckButton _fullscreenCheck = null!;

	public override void _Ready()
	{
		_addressEdit = GetNode<LineEdit>("%AddressEdit");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_resolutionOption = GetNode<OptionButton>("%ResolutionOption");
		_fullscreenCheck = GetNode<CheckButton>("%FullscreenCheck");

		GetNode<Button>("%SoloButton").Pressed += OnSolo;
		GetNode<Button>("%HostButton").Pressed += OnHost;
		GetNode<Button>("%JoinButton").Pressed += OnJoin;
		GetNode<Button>("%QuitButton").Pressed += () => GetTree().Quit();
		GetNode<Button>("%ApplyDisplayButton").Pressed += OnApplyDisplay;

		FillResolutionOptions();

		var session = GetNode<GameSessionAutoload>("/root/GameSession");
		session.SessionEvent += msg => _statusLabel.Text = msg;
		session.SessionStarted += () =>
			GetTree().ChangeSceneToFile("res://scenes/Game.tscn");

		_statusLabel.Text = session.Cards is null
			? "Ошибка загрузки data/cards.json"
			: $"Карт: {session.Cards.Characters.Count} персонажей, {session.Cards.Laws.Count} законов";
	}

	void FillResolutionOptions()
	{
		_resolutionOption.Clear();
		foreach (var preset in DisplaySettings.Presets)
			_resolutionOption.AddItem(preset.Label);

		DisplaySettings.Load();
		_resolutionOption.Selected = DisplaySettings.FindPresetIndex(DisplaySettings.Width, DisplaySettings.Height);
		_fullscreenCheck.ButtonPressed = DisplaySettings.Fullscreen;
		_resolutionOption.Disabled = DisplaySettings.Fullscreen;
		_fullscreenCheck.Toggled += on => _resolutionOption.Disabled = on;
	}

	void OnApplyDisplay()
	{
		var idx = Mathf.Clamp(_resolutionOption.Selected, 0, DisplaySettings.Presets.Length - 1);
		var (w, h, _) = DisplaySettings.Presets[idx];
		var fullscreen = _fullscreenCheck.ButtonPressed;
		DisplaySettings.Save(w, h, fullscreen);
		DisplaySettings.Apply();
		_resolutionOption.Disabled = fullscreen;
		_statusLabel.Text = fullscreen
			? "Полноэкранный режим включён"
			: $"Разрешение: {w}×{h}";
	}

	void OnSolo()
	{
		_statusLabel.Text = "Запуск соло…";
		GetNode<GameSessionAutoload>("/root/GameSession").StartSolo(virtualOpponents: 1);
	}

	void OnHost()
	{
		_statusLabel.Text = "Создание хоста…";
		GetNode<GameSessionAutoload>("/root/GameSession").StartHost(expectedHumans: 2);
	}

	void OnJoin()
	{
		var address = string.IsNullOrWhiteSpace(_addressEdit.Text)
			? "127.0.0.1"
			: _addressEdit.Text.Trim();
		_statusLabel.Text = $"Подключение к {address}…";
		GetNode<GameSessionAutoload>("/root/GameSession").StartClient(address);
	}
}
