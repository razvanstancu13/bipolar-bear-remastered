using UnityEngine;

// A background cloud: drifts `speed` units per frame (the game runs at a fixed 60 fps, see FrameRate) and starts over
// at respawnX once it has passed stopX.
public class CloudBehaviour : MonoBehaviour
{
	public float stopX;
	public float respawnX;
	public float speed;

	void Update()
	{
		var p = transform.position;
		p.x += speed;
		if (p.x < stopX)
			p.x = respawnX;
		transform.position = p;
	}
}
