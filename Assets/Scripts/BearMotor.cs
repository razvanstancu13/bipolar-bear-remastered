using System;
using System.Collections.Generic;
using UnityEngine;

// Moves the bear through the level. Each physics step PlayerController asks for a movement; the bear's box is swept
// along each axis in turn and stopped where it meets something solid. Its BoxCollider2D is only the box's shape (it
// stays disabled, so eskimos walk into the bear instead of being blocked by it); widening it widens the sweep, which
// is how a punch reaches further. Triggers (pills, hazards) are found by an overlap check at the bear's normal size.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class BearMotor : MonoBehaviour
{
	const float SideInset = 0.05f;       // the box is this much narrower across a sweep, so that it does not catch what it slides along
	const float FacingOpposed = -0.5f;   // a hit stops the sweep when its normal points back along it at least this much

	public LayerMask solidLayers = 1;
	public float skin = 0.02f;           // the swept box starts this far inside the real one

	public event Action<Collider2D> Hit;       // something solid it ran into or overlaps, after each move
	public event Action<Collider2D> Touched;   // a trigger it has just entered

	public bool IsGrounded { get; private set; }
	public Vector2 Velocity { get; private set; }

	Rigidbody2D _body;
	BoxCollider2D _box;
	Vector2 _touchSize;
	ContactFilter2D _solids, _everything;
	readonly List<RaycastHit2D> _sweepHits = new List<RaycastHit2D>();
	readonly List<Collider2D> _overlaps = new List<Collider2D>();
	readonly List<Collider2D> _hitThisMove = new List<Collider2D>();
	readonly HashSet<Collider2D> _touching = new HashSet<Collider2D>();
	readonly HashSet<Collider2D> _touchingNow = new HashSet<Collider2D>();

	void Awake()
	{
		_body = GetComponent<Rigidbody2D>();
		_box = GetComponent<BoxCollider2D>();
		_box.enabled = false;
		_touchSize = _box.size;
		_solids = new ContactFilter2D { useTriggers = false };
		_solids.SetLayerMask(solidLayers);
		_everything = new ContactFilter2D { useTriggers = true };
	}

	// moves by delta this physics step, or as far as solids allow
	public void Move(Vector2 delta)
	{
		_hitThisMove.Clear();
		IsGrounded = false;
		Vector2 centre = transform.TransformPoint(_box.offset);
		if (delta.x != 0f)
			delta.x = SweepX(centre, delta.x);
		if (delta.y != 0f)
		{
			bool falling = delta.y < 0f;
			float allowed = SweepY(centre + Vector2.right * delta.x, delta.y);
			IsGrounded = falling && allowed > delta.y;
			delta.y = allowed;
		}
		Velocity = delta / Time.fixedDeltaTime;
		_body.linearVelocity = Velocity;

		foreach (var c in _hitThisMove)
			if (c) Hit?.Invoke(c);
		UpdateTouches(centre + delta);
	}

	Vector2 Size(float width, float height)
	{
		var s = transform.lossyScale;
		return new Vector2(width * Mathf.Abs(s.x), height * Mathf.Abs(s.y));
	}

	float SweepX(Vector2 centre, float dx)
	{
		var size = Size(_box.size.x, _box.size.y) - new Vector2(2f * skin, 2f * SideInset);
		return Sweep(centre, size, new Vector2(Mathf.Sign(dx), 0f), Mathf.Abs(dx)) * Mathf.Sign(dx);
	}

	float SweepY(Vector2 centre, float dy)
	{
		var size = Size(_box.size.x, _box.size.y) - new Vector2(2f * SideInset, 2f * skin);
		return Sweep(centre, size, new Vector2(0f, Mathf.Sign(dy)), Mathf.Abs(dy)) * Mathf.Sign(dy);
	}

	// how far the box may travel along dir, at most distance. A negative result backs it out to the skin. What the box
	// already overlaps only stops it from going further in (so a widened punch box cannot pin the bear to a wall).
	float Sweep(Vector2 centre, Vector2 size, Vector2 dir, float distance)
	{
		float allowed = distance;
		Physics2D.BoxCast(centre, size, 0f, dir, _solids, _sweepHits, distance + skin);
		foreach (var hit in _sweepHits)
		{
			bool overlapping = hit.distance <= 0f;
			if (overlapping)
			{
				Remember(hit.collider);
				if (Vector2.Dot((Vector2)hit.collider.bounds.center - centre, dir) > 0f)
					allowed = Mathf.Min(allowed, 0f);
			}
			else if (Vector2.Dot(hit.normal, dir) < FacingOpposed)
			{
				Remember(hit.collider);
				allowed = Mathf.Min(allowed, hit.distance - skin);
			}
		}
		return allowed;
	}

	void Remember(Collider2D c)
	{
		if (!_hitThisMove.Contains(c))
			_hitThisMove.Add(c);
	}

	void UpdateTouches(Vector2 centre)
	{
		_touchingNow.Clear();
		Physics2D.OverlapBox(centre, Size(_touchSize.x, _touchSize.y), 0f, _everything, _overlaps);
		foreach (var c in _overlaps)
		{
			if (!c.isTrigger) continue;
			_touchingNow.Add(c);
			if (!_touching.Contains(c))
				Touched?.Invoke(c);
		}
		_touching.Clear();
		_touching.UnionWith(_touchingNow);
	}
}
