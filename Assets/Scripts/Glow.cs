using UnityEngine;

// A crystal's glow: its material's emission (set up by Make3D) pulses slowly between dim and bright. Neighbouring
// crystals are out of step.
public class Glow : MonoBehaviour
{
	public Color color = Color.white;               // the emission at full brightness
	public float low = 0.35f, high = 1f;            // brightness range
	public float speed = 2f;                        // radians per second

	Renderer[] _renderers;
	MaterialPropertyBlock _block;
	float _phase;
	static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

	void Start()
	{
		_renderers = GetComponentsInChildren<Renderer>();
		_block = new MaterialPropertyBlock();
		_phase = transform.position.x * 0.7f;
	}

	void Update()
	{
		float k = Mathf.Lerp(low, high, 0.5f + 0.5f * Mathf.Sin(Time.time * speed + _phase));
		foreach (var r in _renderers)
		{
			r.GetPropertyBlock(_block);
			_block.SetColor(EmissionColor, color * k);
			r.SetPropertyBlock(_block);
		}
	}
}
