using UnityEngine;

// The bear's overuse penalties shown over the 3D model: "Z" letters rising while it sleeps, tears falling while it
// cries (the old sprites had them painted in). PlayerController calls Play(); Make3D fills in the references.
public class MoodFx : MonoBehaviour
{
	const float StartDelay = 0.6f;                   // after the sit-down
	const float ZEvery = 0.9f, TearEvery = 0.35f;
	const float TearScale = 0.35f;                   // a tear is this much of `size`

	public Transform head;
	public Sprite zzz;
	public Sprite tear;
	public float size = 1.1f;                        // world height of a Z (tears are smaller)

	float _until, _next;
	Sprite _sprite;
	bool _sleeping;

	public void Play(bool sleep, float seconds)
	{
		_sprite = sleep ? zzz : tear;
		_sleeping = sleep;
		_until = Time.time + seconds;
		_next = Time.time + StartDelay;
	}

	void Update()
	{
		if (!head || !_sprite || Time.time > _until || Time.time < _next) return;
		_next = Time.time + (_sleeping ? ZEvery : TearEvery);
		Emit();
	}

	// a Z drifts up and forward; a tear drops from the eye under gravity
	void Emit()
	{
		var go = new GameObject(_sleeping ? "Zzz" : "Tear") { layer = GameLayers.Characters };
		go.AddComponent<SpriteRenderer>().sprite = _sprite;
		go.transform.localScale = Vector3.one * (size * (_sleeping ? 1f : TearScale) / _sprite.bounds.size.y);
		float face = transform.lossyScale.x < 0f ? -1f : 1f;
		go.transform.position = head.position + new Vector3(face * (_sleeping ? 0.4f : 0.5f), _sleeping ? 0.6f : 0.1f, -1.2f);
		var velocity = _sleeping ? new Vector3(0.6f * face, 1.4f, 0f)
		                         : new Vector3(0.4f * face * Random.Range(0.5f, 1.2f), 0.6f, 0f);
		go.AddComponent<FxFloat>().Init(velocity, _sleeping ? 0f : -9f, _sleeping ? 1.8f : 0.9f);
	}
}
