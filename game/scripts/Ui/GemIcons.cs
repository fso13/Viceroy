using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

public static class GemIcons
{
	static readonly Dictionary<GemColor, Texture2D> Cache = new();

	public static Texture2D Get(GemColor color)
	{
		if (Cache.TryGetValue(color, out var tex))
			return tex;

		var name = color switch
		{
			GemColor.Blue => "blue",
			GemColor.Red => "red",
			GemColor.Green => "green",
			GemColor.Yellow => "yellow",
			_ => "blue"
		};
		var path = $"res://assets/gems/{name}.png";
		tex = ResourceLoader.Exists(path)
			? GD.Load<Texture2D>(path)
			: MakeFallback(color);
		Cache[color] = tex;
		return tex;
	}

	public static TextureRect MakeRect(GemColor color, float size = 28f) =>
		new()
		{
			Texture = Get(color),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			TooltipText = ColorName(color)
		};

	public static string ColorName(GemColor c) => c switch
	{
		GemColor.Blue => "Синий",
		GemColor.Red => "Красный",
		GemColor.Green => "Зелёный",
		GemColor.Yellow => "Жёлтый",
		_ => c.ToString()
	};

	static Texture2D MakeFallback(GemColor color)
	{
		var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
		var c = color switch
		{
			GemColor.Blue => new Color(0.25f, 0.45f, 0.9f),
			GemColor.Red => new Color(0.85f, 0.25f, 0.2f),
			GemColor.Green => new Color(0.25f, 0.7f, 0.35f),
			GemColor.Yellow => new Color(0.9f, 0.75f, 0.2f),
			_ => Colors.Gray
		};
		img.Fill(c);
		return ImageTexture.CreateFromImage(img);
	}
}
