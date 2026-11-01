using UnityEngine;

// Stars circling over a stunned eskimo's head (the 3D stand-in for the old stunned sprite). Its children are the
// stars (set up by Make3D); EnemyLogic switches the object on and off.
public class StunStars : MonoBehaviour
{
	public Transform head;
	public float height = 0.6f;       // above the head bone, world units
	public float radius = 0.7f;
	public float turnsPerSecond = 0.8f;

	void LateUpdate()
	{
		if (!head) return;
		Vector3 c = head.position + Vector3.up * height;
		int n = transform.childCount;
		for (int i = 0; i < n; i++)
		{
			float a = (Time.time * turnsPerSecond + i / (float)n) * Mathf.PI * 2f;
			var s = transform.GetChild(i);
			// a flat ellipse seen from the side camera: x across, z in depth (the far stars go behind the head)
			s.position = c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2f) * 0.08f, Mathf.Sin(a) * radius);
			s.rotation = Quaternion.Euler(0f, Time.time * 200f + i * 60f, 0f);
		}
	}
}
