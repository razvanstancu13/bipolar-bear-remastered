using UnityEngine;

// The roar: its trigger collider flies `speed` units per frame from where the bear stood (so what it hits is as in the
// 2D game) and is removed after `distance` either way. What you see is the `wave` particles (sound-wave arcs pouring
// from the bear's mouth and widening into a cone, set up by Make3D), or else the old picture moved to the head.
public class GrowlBehavior : MonoBehaviour
{
	public float distance = 5;
	public float direction = 1;                      // +1 right, -1 left
	public float speed = 0.2f;
	public ParticleSystem wave;                      // the sound wave (Make3D); left behind at the mouth when it starts
	[HideInInspector] public Transform source;       // the bear's head (set by PlayerController)

	const float MouthAhead = 0.5f;                   // the head bone sits at the back of the head
	const float WaveZ = -3f;                         // in front of the bear model

	float _startX;

	void Start()
	{
		_startX = transform.position.x;
		var sr = GetComponent<SpriteRenderer>();
		if (source && wave)
		{
			StartWave();
			if (sr) sr.enabled = false;
		}
		else if (source && sr)
			MovePictureToHead(sr);
	}

	// the particles stay where the roar came out (world space), pointing the way the bear faces
	void StartWave()
	{
		wave.transform.SetParent(null, true);
		wave.transform.position = new Vector3(source.position.x + direction * MouthAhead, source.position.y, WaveZ);
		wave.transform.rotation = Quaternion.LookRotation(Vector3.right * direction);
		wave.transform.localScale = Vector3.one;
		wave.name = "RoarWave";
		wave.GetComponent<ParticleSystemRenderer>().flip = new Vector3(direction < 0f ? 1f : 0f, 0f, 0f);   // ")" opens forward
		wave.Play();
	}

	// the picture on a child at the head, its near edge just in front of the snout, drawn in front of the bear model
	void MovePictureToHead(SpriteRenderer sr)
	{
		var pic = new GameObject("Wave").AddComponent<SpriteRenderer>();
		pic.sprite = sr.sprite;
		pic.color = sr.color;
		pic.sortingLayerID = sr.sortingLayerID;
		pic.sortingOrder = sr.sortingOrder;
		pic.flipX = direction < 0f;                  // the picture's arcs open to the right
		pic.transform.SetParent(transform, false);
		float half = sr.bounds.extents.x;
		pic.transform.position = new Vector3(source.position.x + direction * half * 1.5f, source.position.y, WaveZ);
		sr.enabled = false;
	}

	void Update()
	{
		var p = transform.position;
		p.x += direction * speed;
		transform.position = p;
		if (Mathf.Abs(p.x - _startX) >= distance)      // either way (fired left it used to fly on for ever)
			Destroy(gameObject);
	}
}
