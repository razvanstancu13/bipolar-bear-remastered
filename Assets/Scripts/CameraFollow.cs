using UnityEngine;

// The level camera eases after the bear, keeping `offset` from it. In step with the bear's physics movement.
public class CameraFollow : MonoBehaviour
{
	public Transform target;
	public float smoothTime = 0.2f;
	public Vector3 offset;

	Vector3 _velocity;

	void FixedUpdate()
	{
		transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref _velocity, smoothTime);
	}
}
