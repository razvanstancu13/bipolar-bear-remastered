using UnityEngine;

// An eskimo: walks back and forth around where it started; when the bear is close in front it stops and thrusts its
// spear (the strike kills the bear unless the bear is punching); a roar (the Growl object) stuns it for a while.
public class EnemyLogic : MonoBehaviour
{
	const float StunSeconds = 3f;
	const float ReachHeight = 2.5f;                  // the bear must be within this much height to be in reach
	const float StrikeExtraReach = 0.3f;             // the spear tip reaches a bit further than where it starts
	public const float DeathSeconds = 0.8f;          // punched: its death clip, then it is gone

	[Header("Walk")]
	public float walkSpeed = 5.2f;                   // set on the body each physics step
	public float moveRadius = 5f;

	[Header("Spear attack (3D version)")]
	public float attackRange = 3.2f;
	public float attackSeconds = 0.8f;               // the EskimoAttack state's length (Make3D sets its speed to match)
	public float strikeFrom = 0.38f, strikeTo = 0.7f; // fraction of the attack when the strike can kill
	public float attackCooldown = 1.0f;
	public AudioClip soundThrust;                    // set by Make3D; played at the camera (the game's sounds are 2D)
	public SpearHold spear;                          // set by Make3D (not a child: it must not be mirrored)

	Animator _animator;
	Rigidbody2D _body;
	SpearHold _spear;
	StunStars _stars;
	PlayerController _player;
	GameLogic _gameLogic;

	float _startX;
	float _direction = 1f;                           // +1 walking right, -1 left
	bool _stunned, _attacking, _dead;

	public bool IsDead => _dead;
	float _stunTimer, _attackTime, _cooldown;

	void Awake()
	{
		_animator = GetComponentInChildren<Animator>();   // the 3D model (child) carries the Animator
		_body = GetComponent<Rigidbody2D>();
		_spear = spear ? spear : GetComponentInChildren<SpearHold>(true);
		_stars = GetComponentInChildren<StunStars>(true);
		ShowStars(false);
	}

	void Start()
	{
		_startX = transform.position.x;
		_player = FindAnyObjectByType<PlayerController>();
		_gameLogic = FindAnyObjectByType<GameLogic>();
	}

	void FixedUpdate()
	{
		if (_dead)
			return;
		if (_stunned)
		{
			_stunTimer -= Time.deltaTime;
			if (_stunTimer <= 0f)
			{
				_stunned = false;
				ShowStars(false);
			}
		}
		else if (!Attack())
			Patrol();
	}

	void OnCollisionEnter2D(Collision2D coll)
	{
		if (_dead || coll.gameObject.name != ObjectNames.Growl)
			return;
		Destroy(coll.gameObject);
		Stun();
	}

	// hit by a roar: stands still with stars over its head (the stunned clip, then dizzy) for a few seconds
	public void Stun()
	{
		_body.linearVelocity = Vector2.zero;
		_stunned = true;
		_stunTimer = StunSeconds;
		_attacking = false;
		SetThrusting(false);
		ShowStars(true);
		AnimStates.Play(_animator, AnimStates.EskimoStunned);
	}

	// punched: it stops, can no longer hurt or block the bear, plays its death clip and disappears (with its spear)
	public void Die()
	{
		if (_dead) return;
		_dead = true;
		_attacking = false;
		SetThrusting(false);
		ShowStars(false);
		_body.linearVelocity = Vector2.zero;
		_body.bodyType = RigidbodyType2D.Kinematic;
		foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
		AnimStates.Play(_animator, AnimStates.EskimoDeath);
		Destroy(gameObject, DeathSeconds);
		if (_spear) Destroy(_spear.gameObject, DeathSeconds);
	}

	// walks to moveRadius on either side of its start, facing the way it goes
	void Patrol()
	{
		var s = transform.localScale;
		if (s.x * _direction > 0f)                       // the eskimo model looks left while scale.x > 0
			transform.localScale = new Vector3(-s.x, s.y, s.z);
		AnimStates.Play(_animator, AnimStates.EskimoWalk);
		float x = transform.position.x;
		if (_direction > 0f ? x > _startX + moveRadius : x < _startX - moveRadius)
			_direction = -_direction;
		_body.linearVelocity = new Vector2(_direction * walkSpeed, 0f);
	}

	// true while the attack runs (the eskimo stands still)
	bool Attack()
	{
		_cooldown -= Time.fixedDeltaTime;
		if (!_attacking)
		{
			if (_cooldown > 0f || !BearInReach(attackRange)) return false;
			_attacking = true;
			_attackTime = 0f;
			_animator.Play(Animator.StringToHash(AnimStates.EskimoAttack), 0, 0f);
			var cam = Camera.main;
			if (soundThrust && cam) AudioSource.PlayClipAtPoint(soundThrust, cam.transform.position);
		}
		_attackTime += Time.fixedDeltaTime;
		float u = _attackTime / attackSeconds;
		SetThrusting(u > 0.2f && u < 0.85f);
		if (u > strikeFrom && u < strikeTo && BearInReach(attackRange + StrikeExtraReach) && _gameLogic && !_player.IsPunching)
			_gameLogic.OnPlayerDead(gameObject);
		if (u >= 1f)
		{
			_attacking = false;
			_cooldown = attackCooldown;
			SetThrusting(false);
		}
		_body.linearVelocity = Vector2.zero;
		return true;                                 // this step was part of the attack (also its last one)
	}

	// the bear in front of it, closer than reach, at about its height
	bool BearInReach(float reach)
	{
		if (!_player) return false;
		Vector2 d = _player.transform.position - transform.position;
		float ahead = d.x * _direction;
		return ahead > 0f && ahead < reach && Mathf.Abs(d.y) < ReachHeight;
	}

	void SetThrusting(bool on)
	{
		if (_spear) _spear.thrusting = on;
	}

	void ShowStars(bool on)
	{
		if (_stars) _stars.gameObject.SetActive(on);
	}
}
