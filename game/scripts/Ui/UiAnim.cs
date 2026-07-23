using Godot;

namespace Namestnik.Ui;

/// <summary>Small tween helpers for UI polish.</summary>
public static class UiAnim
{
	public static void EnsurePivot(Control node)
	{
		var size = node.Size;
		if (size == Vector2.Zero)
			size = node.CustomMinimumSize;
		node.PivotOffset = size * 0.5f;
	}

	public static void PopIn(Control node, float delay = 0f, float fromScale = 0.86f, float duration = 0.22f)
	{
		if (!GodotObject.IsInstanceValid(node))
			return;

		EnsurePivot(node);
		node.Modulate = new Color(1, 1, 1, 0);
		node.Scale = new Vector2(fromScale, fromScale);

		var tween = node.CreateTween();
		if (delay > 0f)
			tween.TweenInterval(delay);
		tween.SetParallel(true);
		tween.TweenProperty(node, "modulate:a", 1f, duration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(node, "scale", Vector2.One, duration)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	public static void OverlayIn(Control dim, Control panel, float duration = 0.26f)
	{
		if (!GodotObject.IsInstanceValid(panel))
			return;

		EnsurePivot(panel);
		if (dim is not null && GodotObject.IsInstanceValid(dim))
		{
			var c = dim.Modulate;
			dim.Modulate = new Color(c.R, c.G, c.B, 0);
		}

		panel.Modulate = new Color(1, 1, 1, 0);
		panel.Scale = new Vector2(0.92f, 0.92f);

		var tween = panel.CreateTween();
		tween.SetParallel(true);
		if (dim is not null && GodotObject.IsInstanceValid(dim))
		{
			tween.TweenProperty(dim, "modulate:a", 1f, duration * 0.85f)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}

		tween.TweenProperty(panel, "modulate:a", 1f, duration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(panel, "scale", Vector2.One, duration)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	public static void PulseOnce(Control node, float peak = 1.06f, float duration = 0.18f)
	{
		if (!GodotObject.IsInstanceValid(node))
			return;

		EnsurePivot(node);
		var tween = node.CreateTween();
		tween.TweenProperty(node, "scale", new Vector2(peak, peak), duration * 0.45f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(node, "scale", Vector2.One, duration * 0.55f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
	}

	public static void HoverScale(Control node, bool hovered, float hover = 1.05f)
	{
		if (!GodotObject.IsInstanceValid(node))
			return;

		EnsurePivot(node);
		var tween = node.CreateTween();
		tween.TweenProperty(node, "scale", hovered ? new Vector2(hover, hover) : Vector2.One, 0.12f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}

	public static Tween SoftPulseLoop(Control node, float minA = 0.55f, float maxA = 1f, float period = 1.1f)
	{
		node.Modulate = new Color(node.Modulate.R, node.Modulate.G, node.Modulate.B, maxA);
		var tween = node.CreateTween();
		tween.SetLoops();
		tween.TweenProperty(node, "modulate:a", minA, period * 0.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(node, "modulate:a", maxA, period * 0.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		return tween;
	}

	public static void FlashModulate(CanvasItem node, Color flash, float duration = 0.4f)
	{
		if (!GodotObject.IsInstanceValid(node))
			return;

		node.Modulate = flash;
		var tween = node.CreateTween();
		tween.TweenProperty(node, "modulate", Colors.White, duration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
}