using Godot;

namespace Namestnik.Ui;

/// <summary>Resolution / fullscreen preferences (persisted to user://display.cfg).</summary>
public static class DisplaySettings
{
	public const string ConfigPath = "user://display.cfg";

	/// <summary>Design resolution used by canvas stretch.</summary>
	public const int BaseWidth = 1280;
	public const int BaseHeight = 720;

	public static readonly (int Width, int Height, string Label)[] Presets =
	[
		(1280, 720, "1280×720"),
		(1600, 900, "1600×900"),
		(1920, 1080, "1920×1080"),
		(2560, 1440, "2560×1440"),
		(3840, 2160, "3840×2160")
	];

	public static int Width { get; private set; } = BaseWidth;
	public static int Height { get; private set; } = BaseHeight;
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

		Width = (int)cfg.GetValue("display", "width", BaseWidth);
		Height = (int)cfg.GetValue("display", "height", BaseHeight);
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

		var win = ((SceneTree)Engine.GetMainLoop()).Root.GetWindow();
		ConfigureContentScale(win);

		if (Fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			return;
		}

		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

		var screen = DisplayServer.WindowGetCurrentScreen();
		var usable = DisplayServer.ScreenGetUsableRect(screen);
		// Leave room for OS chrome; never request a window larger than the screen.
		var maxW = Math.Max(640, usable.Size.X - 24);
		var maxH = Math.Max(360, usable.Size.Y - 24);
		var applyW = Math.Min(Width, maxW);
		var applyH = Math.Min(Height, maxH);

		DisplayServer.WindowSetSize(new Vector2I(applyW, applyH));
		var pos = new Vector2I(
			usable.Position.X + Math.Max(0, (usable.Size.X - applyW) / 2),
			usable.Position.Y + Math.Max(0, (usable.Size.Y - applyH) / 2));
		DisplayServer.WindowSetPosition(pos, screen);
	}

	public static void ConfigureContentScale(Window win)
	{
		win.ContentScaleSize = new Vector2I(BaseWidth, BaseHeight);
		win.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
		win.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
		win.ContentScaleStretch = Window.ContentScaleStretchEnum.Fractional;
		win.MinSize = new Vector2I(960, 540);
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
