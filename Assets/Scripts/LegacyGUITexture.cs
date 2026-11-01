using UnityEngine;

// Stand-in for Unity 4's GUITexture (see LegacyGUI.cs).
public class LegacyGUITexture : MonoBehaviour
{
	public Texture texture;
	public Color color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
	public Rect pixelInset;

	// the area in reference pixels, y up (what GUITexture.GetScreenRect gave at 1280 x 720)
	public Rect ReferenceRect()
	{
		Vector3 p = transform.position, s = transform.lossyScale;
		float w = LegacyGUI.Reference.x, h = LegacyGUI.Reference.y;
		return new Rect(p.x * w - s.x * w / 2 + pixelInset.x, p.y * h - s.y * h / 2 + pixelInset.y,
		                s.x * w + pixelInset.width, s.y * h + pixelInset.height);
	}

	// same as GUITexture.HitTest: true when the screen point (Input.mousePosition) is on the texture
	public bool HitTest(Vector3 screenPoint)
	{
		Rect r = LegacyGUI.ToScreen(ReferenceRect());
		return r.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));
	}

	void OnGUI()
	{
		if (!texture)
			return;
		GUI.depth = LegacyGUI.Depth(transform);
		Color old = GUI.color;
		GUI.color = color * 2f;
		GUI.DrawTexture(LegacyGUI.ToScreen(ReferenceRect()), texture, ScaleMode.StretchToFill);
		GUI.color = old;
	}
}
