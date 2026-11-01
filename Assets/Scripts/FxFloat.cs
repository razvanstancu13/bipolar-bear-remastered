using UnityEngine;

// Moves a little effect sprite along (with gravity for tears), fades it out over its life, then removes it (MoodFx).
public class FxFloat : MonoBehaviour
{
	Vector3 _velocity;
	float _gravity, _life, _age;
	SpriteRenderer _sr;

	public void Init(Vector3 velocity, float gravity, float life)
	{
		_velocity = velocity;
		_gravity = gravity;
		_life = life;
	}

	void Start() => _sr = GetComponent<SpriteRenderer>();

	void Update()
	{
		_age += Time.deltaTime;
		_velocity.y += _gravity * Time.deltaTime;
		transform.position += _velocity * Time.deltaTime;
		if (_sr)
		{
			var c = _sr.color;
			c.a = Mathf.Clamp01(2f * (1f - _age / _life));   // full for the first half, then fading
			_sr.color = c;
		}
		if (_age >= _life) Destroy(gameObject);
	}
}
