using UnityEngine;

// Turns the object around its vertical axis (pills, coin) - the 3D stand-in for the old sprite spin animation.
public class Spin : MonoBehaviour
{
	public float degreesPerSecond = 120f;
	public float bobHeight = 0.15f;
	public float bobSpeed = 2f;

	Vector3 _start;
	float _phase;

	void Start()
	{
		_start = transform.localPosition;
		_phase = transform.position.x;   // neighbours do not bob in step
	}

	void Update()
	{
		transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
		transform.localPosition = _start + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight);
	}
}
