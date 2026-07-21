using Godot;
using Namestnik.Core.Models;

namespace Namestnik.Ui;

/// <summary>Shared colored reward/cost glyphs used on cards and in the legend.</summary>
public static class CardSymbolPainter
{
	public static readonly Color Ink = new(0.92f, 0.88f, 0.78f);

	public static Color GemTint(GemColor c) => c switch
	{
		GemColor.Blue => new Color(0.3f, 0.5f, 0.95f),
		GemColor.Red => new Color(0.9f, 0.28f, 0.22f),
		GemColor.Green => new Color(0.28f, 0.75f, 0.38f),
		GemColor.Yellow => new Color(0.95f, 0.78f, 0.2f),
		_ => Colors.Gray
	};

	public static void DrawCostGem(CanvasItem canvas, Vector2 center, float radius, GemColor color)
	{
		canvas.DrawCircle(center, radius, GemTint(color));
		canvas.DrawArc(center, radius, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.45f), 1.2f);
	}

	public static void DrawVp(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		var c = p + new Vector2(s * 0.5f, s * 0.5f);
		var wreath = new Color(0.45f, 0.7f, 0.35f);
		canvas.DrawArc(c, s * 0.42f, -0.6f, Mathf.Pi + 0.6f, 12, wreath, s * 0.12f);
		canvas.DrawArc(c, s * 0.42f, Mathf.Pi - 0.6f, Mathf.Tau + 0.6f, 12, wreath, s * 0.12f);
		canvas.DrawString(font, c + new Vector2(0, fs * 0.35f), amount.ToString(),
			HorizontalAlignment.Center, -1, fs, new Color(0.95f, 0.9f, 0.4f));
	}

	public static void DrawGems(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		var c = p + new Vector2(s * 0.35f, s * 0.5f);
		DrawDiamond(canvas, c, s * 0.28f, new Color(0.55f, 0.7f, 1f));
		canvas.DrawString(font, p + new Vector2(s * 0.7f, s * 0.72f), amount.ToString(),
			HorizontalAlignment.Left, -1, fs, Ink);
	}

	public static void DrawCardIcon(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		var col = new Color(0.75f, 0.65f, 0.4f);
		var rect = new Rect2(p + new Vector2(s * 0.1f, s * 0.15f), new Vector2(s * 0.55f, s * 0.7f));
		canvas.DrawRect(rect, col, false, 1.5f);
		canvas.DrawRect(new Rect2(rect.Position + new Vector2(s * 0.08f, s * 0.12f), rect.Size * 0.55f),
			new Color(0.75f, 0.65f, 0.4f, 0.35f), true);
		if (amount > 1)
			canvas.DrawString(font, p + new Vector2(s * 0.75f, s * 0.72f), amount.ToString(),
				HorizontalAlignment.Left, -1, fs, Ink);
	}

	public static void DrawScience(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		// Наука = шестерёнка (не путать со свитком магии).
		var c = p + new Vector2(s * 0.4f, s * 0.5f);
		DrawGear(canvas, c, s * 0.38f, s * 0.22f, s * 0.1f, 8,
			new Color(0.75f, 0.78f, 0.82f),
			new Color(0.35f, 0.38f, 0.42f));
		if (amount > 1)
			canvas.DrawString(font, p + new Vector2(s * 0.78f, s * 0.72f), amount.ToString(),
				HorizontalAlignment.Left, -1, fs, Ink);
	}

	/// <summary>Science token — cogwheel.</summary>
	public static void DrawGear(
		CanvasItem canvas,
		Vector2 center,
		float outerR,
		float valleyR,
		float hubR,
		int teeth,
		Color fill,
		Color rim)
	{
		const int stepsPerTooth = 6;
		var pts = new Vector2[teeth * stepsPerTooth];
		for (var i = 0; i < teeth; i++)
		{
			var baseA = i * Mathf.Tau / teeth;
			var step = Mathf.Tau / teeth;
			// Tooth profile: valley → rise → tip → tip → fall → valley
			float[] t = [0f, 0.18f, 0.32f, 0.68f, 0.82f, 1f];
			float[] r = [valleyR, valleyR, outerR, outerR, valleyR, valleyR];
			for (var k = 0; k < stepsPerTooth; k++)
			{
				var a = baseA + step * t[k];
				pts[i * stepsPerTooth + k] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r[k];
			}
		}

		canvas.DrawColoredPolygon(pts, fill);
		canvas.DrawArc(center, valleyR * 0.95f, 0, Mathf.Tau, 20, rim, Mathf.Max(1.2f, outerR * 0.1f));
		canvas.DrawCircle(center, hubR, new Color(0.1f, 0.1f, 0.12f));
		canvas.DrawCircle(center, hubR * 0.45f, fill);
		canvas.DrawArc(center, hubR, 0, Mathf.Tau, 14, rim, 1.2f);
	}

	public static void DrawMagic(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		// Магия = свиток.
		DrawScroll(canvas, p, s, amount, font, fs);
	}

	public static void DrawScroll(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		var col = new Color(0.88f, 0.72f, 0.38f);
		var body = new Rect2(p + new Vector2(s * 0.12f, s * 0.28f), new Vector2(s * 0.55f, s * 0.4f));
		canvas.DrawRect(body, col, true);
		canvas.DrawCircle(new Vector2(body.Position.X, body.Position.Y + body.Size.Y * 0.5f), body.Size.Y * 0.5f, col);
		canvas.DrawCircle(new Vector2(body.End.X, body.Position.Y + body.Size.Y * 0.5f), body.Size.Y * 0.5f, col);
		canvas.DrawLine(
			new Vector2(body.Position.X + body.Size.X * 0.25f, body.Position.Y + body.Size.Y * 0.35f),
			new Vector2(body.Position.X + body.Size.X * 0.75f, body.Position.Y + body.Size.Y * 0.35f),
			new Color(0.55f, 0.4f, 0.2f), 1f);
		if (amount > 1)
			canvas.DrawString(font, p + new Vector2(s * 0.8f, s * 0.72f), amount.ToString(),
				HorizontalAlignment.Left, -1, fs, Ink);
	}

	public static void DrawShield(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		var col = new Color(0.55f, 0.85f, 0.65f);
		var top = p + new Vector2(s * 0.4f, s * 0.12f);
		var pts = new[]
		{
			top + new Vector2(-s * 0.28f, s * 0.08f),
			top + new Vector2(s * 0.28f, s * 0.08f),
			top + new Vector2(s * 0.28f, s * 0.4f),
			top + new Vector2(0, s * 0.7f),
			top + new Vector2(-s * 0.28f, s * 0.4f)
		};
		canvas.DrawColoredPolygon(pts, col);
		if (amount > 1)
			canvas.DrawString(font, p + new Vector2(s * 0.75f, s * 0.72f), amount.ToString(),
				HorizontalAlignment.Left, -1, fs, Ink);
	}

	public static void DrawAttack(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		// Атака = меч.
		var col = new Color(0.85f, 0.88f, 0.92f);
		var hilt = new Color(0.7f, 0.45f, 0.25f);
		var tip = p + new Vector2(s * 0.72f, s * 0.18f);
		var baseBlade = p + new Vector2(s * 0.28f, s * 0.62f);
		var dir = (tip - baseBlade).Normalized();
		var perp = new Vector2(-dir.Y, dir.X) * s * 0.07f;
		canvas.DrawColoredPolygon(new[]
		{
			tip,
			baseBlade + perp,
			baseBlade - perp
		}, col);
		var guard = baseBlade;
		canvas.DrawLine(guard - perp * 2.2f, guard + perp * 2.2f, hilt, s * 0.08f);
		canvas.DrawLine(guard, guard - dir * s * 0.22f, hilt, s * 0.1f);
		if (amount > 1)
			canvas.DrawString(font, p + new Vector2(s * 0.78f, s * 0.72f), amount.ToString(),
				HorizontalAlignment.Left, -1, fs, Ink);
	}

	public static void DrawInfinite(CanvasItem canvas, Vector2 p, float s, GemColor color, Font font, int fs)
	{
		canvas.DrawCircle(p + new Vector2(s * 0.32f, s * 0.5f), s * 0.28f, GemTint(color));
		canvas.DrawString(font, p + new Vector2(s * 0.75f, s * 0.72f), "∞",
			HorizontalAlignment.Left, -1, fs + 2, Ink);
	}

	public static void DrawBonusMagic(CanvasItem canvas, Vector2 p, float s, int amount, Font font, int fs)
	{
		DrawMagic(canvas, p, s * 0.85f, 1, font, fs);
		canvas.DrawString(font, p + new Vector2(s * 0.55f, s * 0.78f), $"+{amount}",
			HorizontalAlignment.Left, -1, fs, new Color(1f, 0.65f, 0.9f));
	}

	public static void DrawBonusCircle(CanvasItem canvas, Vector2 p, float s, GemColor color, int amount, Font font, int fs)
	{
		var c = p + new Vector2(s * 0.32f, s * 0.5f);
		canvas.DrawArc(c, s * 0.28f, 0, Mathf.Tau, 18, GemTint(color), s * 0.1f);
		canvas.DrawString(font, p + new Vector2(s * 0.6f, s * 0.72f), $"+{amount}",
			HorizontalAlignment.Left, -1, fs, GemTint(color));
	}

	public static void DrawChoiceSlash(CanvasItem canvas, Vector2 p, float s, Font font, int fs)
	{
		canvas.DrawString(font, p + new Vector2(s * 0.15f, s * 0.72f), "/",
			HorizontalAlignment.Left, -1, fs + 2, new Color(0.7f, 0.65f, 0.55f));
	}

	public static void DrawDiamond(CanvasItem canvas, Vector2 c, float r, Color color)
	{
		var pts = new[]
		{
			c + new Vector2(0, -r),
			c + new Vector2(r * 0.7f, 0),
			c + new Vector2(0, r),
			c + new Vector2(-r * 0.7f, 0)
		};
		canvas.DrawColoredPolygon(pts, color);
	}

	public static void DrawReward(CanvasItem canvas, Reward reward, Vector2 origin, float iconSize, Font font, int fontSize)
	{
		switch (reward)
		{
			case VpReward vp:
				DrawVp(canvas, origin, iconSize, vp.Amount, font, fontSize);
				break;
			case GemsReward g:
				DrawGems(canvas, origin, iconSize, g.Amount, font, fontSize);
				break;
			case CardReward c:
				DrawCardIcon(canvas, origin, iconSize, c.Amount, font, fontSize);
				break;
			case ScienceReward s:
				DrawScience(canvas, origin, iconSize, s.Amount, font, fontSize);
				break;
			case MagicReward m:
				DrawMagic(canvas, origin, iconSize, m.Amount, font, fontSize);
				break;
			case DefenseReward d:
				DrawShield(canvas, origin, iconSize, d.Amount, font, fontSize);
				break;
			case AttackReward a:
				DrawAttack(canvas, origin, iconSize, a.Amount, font, fontSize);
				break;
			case InfiniteReward inf:
				DrawInfinite(canvas, origin, iconSize, inf.Color, font, fontSize);
				break;
			case BonusMagicReward b:
				DrawBonusMagic(canvas, origin, iconSize, b.Amount, font, fontSize);
				break;
			case BonusCircleReward bc:
				DrawBonusCircle(canvas, origin, iconSize, bc.Color, bc.Amount, font, fontSize);
				break;
		}
	}
}
