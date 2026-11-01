using UnityEngine;

// Keeps the eskimo's spear in its right hand: gripped at `grip` along the shaft, pointing the way the eskimo faces,
// tip raised while walking (as in the old sprite), level during the thrust. Set up by Make3D.
public class SpearHold : MonoBehaviour
{
	public Transform hand;            // the RightHand bone
	public Transform body;            // the enemy root (its scale.x flips it)
	public bool nativeLeft = true;    // body.scale.x > 0 = facing left (the old eskimo sprite looked left)
	public float walkTilt = 25f;      // tip up, degrees
	public float thrustTilt = -4f;
	public float grip = 0.4f;         // grip point: fraction of the length from the butt
	public float length = 3f;         // world length of the spear (set by Make3D)

	[HideInInspector] public bool thrusting;
	float _tilt;

	void LateUpdate()
	{
		if (!body || !hand) { Destroy(gameObject); return; }   // the eskimo was punched away
		float face = (body.localScale.x > 0f) == nativeLeft ? -1f : 1f;
		_tilt = Mathf.MoveTowards(_tilt, thrusting ? thrustTilt : walkTilt, 240f * Time.deltaTime);
		Vector3 dir = Quaternion.AngleAxis(_tilt * face, Vector3.forward) * new Vector3(face, 0f, 0f);
		// the spear model's long axis is its local X (Make3D lines it up), tip at +X
		transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
		transform.position = hand.position + dir * (length * (0.5f - grip));
	}
}
