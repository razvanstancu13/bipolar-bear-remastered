using UnityEngine;

// A comic hit burst: a sprite that pops up (grows from 70 % to full size, a quick turn) and fades out, then is gone.
public class ImpactBurst : MonoBehaviour
{
	SpriteRenderer _sr;
	float _size, _seconds, _age, _spin;

	public static void Spawn(Sprite sprite, Vector3 position, float size, float seconds)
	{
		var go = new GameObject("Impact") { layer = GameLayers.Characters };
		go.transform.position = position;
		go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
		var b = go.AddComponent<ImpactBurst>();
		b._sr = go.AddComponent<SpriteRenderer>();
		b._sr.sprite = sprite;
		b._sr.sortingOrder = 100;
		b._size = size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
		b._seconds = seconds;
		b._spin = Random.Range(-90f, 90f);
		b.Apply();
	}

	void Update()
	{
		_age += Time.deltaTime;
		if (_age >= _seconds) { Destroy(gameObject); return; }
		Apply();
	}

	void Apply()
	{
		float u = _age / _seconds;
		transform.localScale = Vector3.one * _size * Mathf.Lerp(0.7f, 1f, Mathf.Sqrt(u));
		transform.Rotate(0f, 0f, _spin * Time.deltaTime);
		var c = _sr.color;
		c.a = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;          // full, then fading over the last 40 %
		_sr.color = c;
	}
}
