using Godot;

namespace Namestnik.Ui;

/// <summary>Resolution / fullscreen preferences (persisted to user://display.cfg).</summary>
public static class DisplaySettings
{
	public const string ConfigPath = "user://display.cfg";

	public static readonly (int Width, int Height, string Label)[] Presets =
	[
		(1280, 720, "1280×720"),
		(1600, 900, "1600×900"),
		(1920, 1080, "1920×1080"),
		(2560, 1440, "2560×1440"),
		(3840, 2160, "3840×2160")
	];

	public static int Width { get; private set; } = 1280;
	public static int Height { get; private set; } = 720;
	public static bool Fullscreen { get; private set; }

	public static void LoadAndApply()
	{
		Load();
		Apply();
	}

	public static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(ConfigPath) != Error.Ok)
			return;

		Width = (int)cfg.GetValue("display", "width", 1280);
		Height = (int)cfg.GetValue("display", "height", 720);
		Fullscreen = (bool)cfg.GetValue("display", "fullscreen", false);
	}

	public static void Save(int width, int height, bool fullscreen)
	{
		Width = width;
		Height = height;
		Fullscreen = fullscreen;

		var cfg = new ConfigFile();
		cfg.SetValue("display", "width", width);
		cfg.SetValue("display", "height", height);
		cfg.SetValue("display", "fullscreen", fullscreen);
		cfg.Save(ConfigPath);
	}

	public static void Apply(int? width = null, int? height = null, bool? fullscreen = null)
	{
		if (width is int w)
			Width = w;
		if (height is int h)
			Height = h;
		if (fullscreen is bool fs)
			Fullscreen = fs;

		if (Fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			return;
		}

		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetSize(new Vector2I(Width, Height));

		var screen = DisplayServer.WindowGetCurrentScreen();
		var screenSize = DisplayServer.ScreenGetSize(screen);
		var pos = new Vector2I(
			Math.Max(0, (screenSize.X - Width) / 2),
			Math.Max(0, (screenSize.Y - Height) / 2));
		DisplayServer.WindowSetPosition(pos, screen);
	}

	public static int FindPresetIndex(int width, int height)
	{
		for (var i = 0; i < Presets.Length; i++)
		{
			if (Presets[i].Width == width && Presets[i].Height == height)
				return i;
		}

		return 0;
	}
}
