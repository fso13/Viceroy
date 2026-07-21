using Godot;

namespace Namestnik.Ui;

/// <summary>Legacy PNG loader kept only as a fallback stub (faces are procedural now).</summary>
public static class CardArt
{
	static Texture2D? _missing;

	public static Texture2D Missing()
	{
		if (_missing is not null)
			return _missing;
		var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
		img.Fill(new Color(0.25f, 0.2f, 0.15f));
		_missing = ImageTexture.CreateFromImage(img);
		return _missing;
	}
}
