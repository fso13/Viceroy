using Godot;

namespace Namestnik.Ui;

/// <summary>Loads cropped card textures from res://assets/cards/.</summary>
public static class CardArt
{
	static readonly Dictionary<int, Texture2D> Cache = new();
	static Texture2D? _back;
	static Texture2D? _missing;

	public static Texture2D Get(int definitionId)
	{
		if (Cache.TryGetValue(definitionId, out var cached))
			return cached;

		var path = $"res://assets/cards/{definitionId:D3}.png";
		var tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		tex ??= Missing();
		Cache[definitionId] = tex;
		return tex;
	}

	public static Texture2D Back()
	{
		if (_back is not null)
			return _back;
		const string path = "res://assets/cards/back.png";
		_back = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : Missing();
		return _back!;
	}

	static Texture2D Missing()
	{
		if (_missing is not null)
			return _missing;
		var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
		img.Fill(new Color(0.25f, 0.2f, 0.15f));
		_missing = ImageTexture.CreateFromImage(img);
		return _missing;
	}
}
