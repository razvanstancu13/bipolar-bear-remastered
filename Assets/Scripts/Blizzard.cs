using UnityEngine;

// Now and then a short blizzard sweeps across the screen from a random direction (on the level camera, set up by
// Make3D): snow streaks blow in from one edge for a few seconds, easing in and out.
public class Blizzard : MonoBehaviour
{
	public ParticleSystem snow;
	public float minGap = 20f, maxGap = 45f;        // seconds of calm between storms
	public float minLength = 4f, maxLength = 7f;    // seconds a storm lasts
	public float rate = 420f;                       // streaks per second at full strength
	public float speed = 18f;
	public float reach = 22f;                       // the emitting edge's distance from the view's middle

	float _next, _end, _length;

	public bool Blowing => Time.time < _end;

	void Start()
	{
		_next = Time.time + Random.Range(minGap, maxGap);
		_end = 0f;
		SetRate(0f);
		snow.Play();
	}

	// a storm now (tests; also what the timer calls)
	public void StartNow()
	{
		_length = Random.Range(minLength, maxLength);
		_end = Time.time + _length;
		_next = _end + Random.Range(minGap, maxGap);
		// from any side or from above, never from below: the streaks fly along `dir` (sideways or downwards, in the
		// camera's plane), emitted from the opposite edge
		float a = Random.Range(Mathf.PI, Mathf.PI * 2f);
		var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
		snow.transform.localPosition = -dir * reach + Vector3.forward * 6f;
		snow.transform.localRotation = Quaternion.LookRotation(dir, Vector3.forward);   // the shape emits along its +Z
		var main = snow.main;
		main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
		main.startLifetime = 2.6f * reach / speed;
	}

	void Update()
	{
		if (Time.time >= _next && !Blowing) StartNow();
		if (!Blowing) { SetRate(0f); return; }
		float t = _length - (_end - Time.time);           // seconds into the storm
		float ease = Mathf.Clamp01(Mathf.Min(t, _end - Time.time) / 1f);   // 1 s in, 1 s out
		SetRate(rate * ease);
	}

	void SetRate(float r)
	{
		var e = snow.emission;
		e.rateOverTime = r;
	}
}
