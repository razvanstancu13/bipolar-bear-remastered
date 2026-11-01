using UnityEngine;

// Stand-ins for Unity 4's GUITexture and GUIText, which newer Unity removed. They keep the old rules: the object's
// position is a point on the screen (0..1), pixelInset / pixelOffset are pixels from there, y counts from the bottom,
// a colour of 0.5 grey means "unchanged", and a higher z is drawn on top. The game was laid out for 1920 x 1080,
// so those pixels are scaled to the real window size.
public static class LegacyGUI
{
	public static readonly Vector2 Reference = new Vector2(1920, 1080);

	// Unity 4 rect (reference pixels, y up) -> GUI rect on the real screen (y down)
	public static Rect ToScreen(Rect r)
	{
		float sx = Screen.width / Reference.x, sy = Screen.height / Reference.y;
		return new Rect(r.x * sx, Screen.height - (r.y + r.height) * sy, r.width * sx, r.height * sy);
	}

	public static int Depth(Transform t) => -Mathf.RoundToInt(t.position.z * 10);
}
