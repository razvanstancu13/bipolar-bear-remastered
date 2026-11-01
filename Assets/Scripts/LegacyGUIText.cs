using UnityEngine;

// Stand-in for Unity 4's GUIText (see LegacyGUI.cs).
public class LegacyGUIText : MonoBehaviour
{
	[TextArea] public string text = "";
	public Font font;
	public int fontSize = 0;
	public Color color = Color.white;
	public Vector2 pixelOffset;

	GUIStyle _style;

	void OnGUI()
	{
		if (string.IsNullOrEmpty(text))
			return;
		if (_style == null)
			_style = new GUIStyle(GUI.skin.label) { font = font, wordWrap = false, richText = true, alignment = TextAnchor.UpperLeft };
		float scale = Screen.height / LegacyGUI.Reference.y;
		_style.fontSize = Mathf.RoundToInt((fontSize > 0 ? fontSize : 16) * scale);
		_style.normal.textColor = color;
		GUI.depth = LegacyGUI.Depth(transform);
		// anchor "upper left": the text's top-left corner sits on the point
		Vector3 p = transform.position;
		Rect anchor = LegacyGUI.ToScreen(new Rect(p.x * LegacyGUI.Reference.x + pixelOffset.x, p.y * LegacyGUI.Reference.y + pixelOffset.y, 0, 0));
		Vector2 size = _style.CalcSize(new GUIContent(text));
		GUI.Label(new Rect(anchor.x, anchor.y, size.x, size.y), text, _style);
	}
}
