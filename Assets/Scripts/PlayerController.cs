using UnityEngine;

// the bear's three moods; each gives one ability (same numbers: PlayerAbility = (PlayerAbility)PlayerState)
public enum PlayerState : int { None = 0, Aggressive, Hysteric, Ecstatic };
public enum PlayerAbility : int { None = 0, Punch, Growl, SuperJump };

// what the player asks for this frame (Update reads the keys, FixedUpdate acts on them)
public struct PlayerInput
{
	public bool Right, Left, Jump, Action, Secondary, Consume;
}

// The bear: walks and jumps (BearMotor), changes mood every few seconds (each mood has its ability:
// punch, roar, super jump), can take a pill for a second ability, and is put to sleep (roar) or made to cry (punch)
// when it overuses the ability the level picked as its weakness. Field names are kept as the scenes serialise them.
public class PlayerController : MonoBehaviour
{
	const float RoarSeconds = 1.2f, PunchSeconds = 0.3f;      // how long an ability blocks the next one
	const float PenaltySeconds = 5f;                          // sleep / cry
	const int PenaltyAfterUses = 5;                           // the weakness used more often than this -> penalty
	const float PunchReach = 2f, NormalReach = 1f;            // collider width while punching / otherwise
	public const float ImpactSeconds = 0.3f;                  // the hit shown (pose frozen) before the death clip
	const float InFrontGap = 0.05f;                           // a killed bear's model this far in front of its killer's

	[Header("Weakness: overusing this ability puts the bear to sleep / makes it cry")]
	public PlayerAbility randomCriteria;
	public int randomCriteriaCounter = 0;

	[Header("HUD pictures: mood avatars")]
	public Texture2D textureAggressive;
	public Texture2D textureHysteric;
	public Texture2D textureEcstatic;

	[Header("HUD pictures: ability labels (Space = the mood's ability, Ctrl = the pill's)")]
	public Texture2D textureTextPunchCtrl;
	public Texture2D textureTextPunchSpace;
	public Texture2D textureTextGrawlCtrl;
	public Texture2D textureTextGrawlSpace;
	public Texture2D textureTextSuperJumpW;

	[Header("Sounds")]
	public AudioClip soundJump;
	public AudioClip soundCollectPill;
	public AudioClip soundPunch;
	public AudioClip soundGrawl;
	public AudioClip soundStep;                               // (not played)
	public AudioClip soundDeath;
	public AudioClip soundSuperJump;                          // the 3D version's new sounds (Make3D fills them in)
	public AudioClip soundSleep;
	public AudioClip soundCry;

	[Header("Movement")]
	public float gravity = -25f;
	public float runSpeed = 8f;
	public float groundDamping = 20f;                         // how fast it changes direction on the ground
	public float inAirDamping = 5f;
	public float jumpHeight = 3f;
	public float jumpBonus = 3f;                              // added while super jump is on

	[Header("State")]
	public bool inAction = false;                             // an ability is running (roar / punch)
	public PlayerState _currentState;
	public PlayerAbility _currentAbility;
	public PlayerAbility _extraAbility;                       // from a pill
	public bool hasExtraAbility = false;
	public int numberOfPills = 0;

	[Header("Prefabs and effects")]
	public Transform growlPrefab;
	public Sprite impactSprite;                               // the hit burst when something kills the bear (Make3D)
	public ParticleSystem punchFx;                            // streaks across the punch's reach (Make3D)

	[Header("Clip timing (measured by Make3D)")]
	public float jumpTakeoff, jumpLanding;                    // the Jump clip's airborne span, normalised
	public float jumpClipSeconds;
	public float roarClipStart;                               // the Roar clip starts here (after its wind-up)

	[Header("HUD objects")]
	public Transform secondaryIndicator;
	public Transform secondaryTextIndicator;
	public Transform stateIndicator;
	public Transform stateTextIndicator;
	public Transform counterText;
	public Transform pillCounterText;

	// test runs (GameTestRun) change what the keys gave; null in normal play
	public System.Func<PlayerInput, PlayerInput> inputOverride;

	// a punch is running, from Space or from a pill (it knocks eskimos away and wins against the spear)
	public bool IsPunching => inAction && _activeAbility == PlayerAbility.Punch;
	public bool IsDead => _dead;

	GameLogic _gameLogic;
	BearMotor _motor;
	Animator _animator;
	AudioSource _audio;
	BoxCollider2D _collider;
	MoodFx _moodFx;
	LegacyGUITexture _moodAvatar, _moodLabel, _pillAvatar, _pillLabel;
	LegacyGUIText _timerText, _pillText;

	PlayerInput _input;
	PlayerAbility _activeAbility;                             // the ability inAction is running
	Vector3 _velocity;
	float _horizontal;                                        // -1, 0, 1: the direction walked
	float _actionTimer, _moodTimer, _extraAbilityTimer, _penaltyTimer;
	bool _penalty, _dead;
	GameObject _killer;                                       // what killed it: kept in front of (see InFrontOf)
	Rigidbody2D _body;
	float _impactTimer;
	const float ImpactZ = -4f;                                // in front of every model
	Vector2 _restPosition;                                    // where a killed bear lies


	void Awake()
	{
		_gameLogic = FindAnyObjectByType<GameLogic>();
		_animator = GetComponentInChildren<Animator>();          // the 3D model (child) carries the Animator
		_motor = GetComponent<BearMotor>();
		_audio = GetComponent<AudioSource>();
		_collider = GetComponent<BoxCollider2D>();
		_body = GetComponent<Rigidbody2D>();
		_moodFx = GetComponent<MoodFx>();
		_moodAvatar = stateIndicator.GetComponent<LegacyGUITexture>();
		_moodLabel = stateTextIndicator.GetComponent<LegacyGUITexture>();
		_pillAvatar = secondaryIndicator.GetComponent<LegacyGUITexture>();
		_pillLabel = secondaryTextIndicator.GetComponent<LegacyGUITexture>();
		_timerText = counterText.GetComponent<LegacyGUIText>();
		_pillText = pillCounterText.GetComponent<LegacyGUIText>();

		_motor.Hit += OnHit;
		_motor.Touched += OnTouch;
	}

	void Start()
	{
		randomCriteria = (PlayerAbility)Random.Range(1, 3);      // the weakness: punch or roar
		NextMood();
	}

	// ---------- collisions ----------

	void OnHit(Collider2D col)
	{
		var other = col.gameObject;
		if (other.name == ObjectNames.Enemy)
		{
			if (IsPunching)
			{
				var eskimo = other.GetComponent<EnemyLogic>();
				if (eskimo) eskimo.Die(); else Destroy(other);
			}
			else
				_gameLogic.OnPlayerDead(other);
		}
		else if (other.name == ObjectNames.Coin)
		{
			_gameLogic.OnCoinCollected();
			Destroy(other);
		}
	}

	void OnTouch(Collider2D col)
	{
		string name = col.gameObject.name;
		if (name == ObjectNames.Pill)
		{
			numberOfPills++;
			_pillText.text = numberOfPills.ToString();
			_audio.PlayOneShot(soundCollectPill, 1f);
			Destroy(col.gameObject);
		}
		else if (ObjectNames.IsHazard(name))
			_gameLogic.OnPlayerDead(col.gameObject);
	}

	// ---------- input (Update) ----------

	void Update()
	{
		if (_dead)
			return;
		if (Input.GetKeyDown(KeyCode.Escape))
			UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Menu);

		// a jump press stays until FixedUpdate has used it (FixedUpdate may not run every frame)
		_input.Jump = _input.Jump || Input.GetKeyDown(KeyCode.W);
		_input.Right = Input.GetKey(KeyCode.D);
		_input.Left = Input.GetKey(KeyCode.A);
		_input.Action = Input.GetKey(KeyCode.Space);
		_input.Consume = Input.GetKey(KeyCode.Return);
		_input.Secondary = Input.GetKey(KeyCode.LeftControl);
		if (inputOverride != null)
			_input = inputOverride(_input);
	}

	// ---------- the physics step ----------

	void FixedUpdate()
	{
		if (_dead)
		{
			if (_impactTimer > 0f)
			{
				_impactTimer -= Time.fixedDeltaTime;
				Pin();
				if (_impactTimer <= 0f) StartDeathClip();
				return;
			}
			// put down already when something killed it (another controller step would push it up out of the eskimo
			// it overlaps); otherwise it falls
			if (_killer)
			{
				Pin();
				InFrontOf(_killer);                              // (the lying pose reaches further back)
			}
			else FallDead();
			return;
		}
		UpdatePenalty();
		float facing = transform.localScale.x < 0f ? -1f : 1f;   // before Walk turns it (the roar goes this way)
		if (!_penalty)
		{
			_velocity = _motor.Velocity;
			if (_motor.IsGrounded)
				_velocity.y = 0;
			Walk();
			Jump();
			UpdatePillAbility();
			Act(facing);
		}
		else
		{
			_velocity = Vector3.zero;
			_horizontal = 0f;                                    // asleep / crying: it stops where it is
		}
		Move();
		_input.Jump = false;
		UpdateMood();
	}

	// killed: the death clip and sound, no more control; GameLogic restarts the level a moment later
	public void Die(GameObject killer = null)
	{
		if (_dead) return;
		_dead = true;
		_input = default;
		_velocity = Vector3.zero;
		_horizontal = 0f;
		_killer = killer;
		// first the hit: the pose freezes where it was struck and a burst shows at the contact
		_restPosition = transform.position;
		Pin();
		_impactTimer = ImpactSeconds;
		_animator.speed = 0f;
		ShowImpact(killer);
		if (soundDeath) _audio.PlayOneShot(soundDeath, 1f);
	}

	// after the hit: down on the ground under it (if something killed it), then the death clip
	void StartDeathClip()
	{
		_animator.speed = 1f;
		if (_killer)
		{
			DropToGround();
			_restPosition = transform.position;
			Pin();
		}
		AnimStates.Play(_animator, AnimStates.Death);
	}

	// the burst halfway between the bear's middle and the nearest point of what hit it, in front of everything
	void ShowImpact(GameObject killer)
	{
		if (!impactSprite) return;
		var ls = transform.lossyScale;
		Vector2 centre = transform.TransformPoint(_collider.offset);
		Vector2 at = centre;
		var kc = killer ? killer.GetComponent<Collider2D>() : null;
		if (kc && kc.enabled)
			at = Vector2.Lerp(centre, kc.bounds.ClosestPoint(centre), 0.5f);
		ImpactBurst.Spawn(impactSprite, new Vector3(at.x, at.y, ImpactZ), _collider.size.y * Mathf.Abs(ls.y), ImpactSeconds);
	}

	// dead: falls to the ground and stays there
	void FallDead()
	{
		_velocity = new Vector3(0f, _motor.IsGrounded ? 0f : _velocity.y + gravity * Time.fixedDeltaTime, 0f);
		_motor.Move(_velocity * Time.fixedDeltaTime);
	}

	// BearMotor moves the bear through its (kinematic) body: a pending MovePosition and the last velocity would
	// carry a killed bear off where it lies
	void Pin()
	{
		transform.position = new Vector3(_restPosition.x, _restPosition.y, transform.position.z);
		_body.linearVelocity = Vector2.zero;
		_body.position = _restPosition;
		_body.MovePosition(_restPosition);
	}

	// killed by an obstacle or an eskimo it landed on: straight down onto the ground or platform under it (through
	// the killer), so it does not die in the air or standing on the eskimo
	void DropToGround()
	{
		// the box from the collider's own size and offset (BearMotor keeps the collider disabled)
		var ls = transform.lossyScale;
		Vector2 centre = transform.TransformPoint(_collider.offset);
		float bottom = centre.y - _collider.size.y * Mathf.Abs(ls.y) * 0.5f;
		foreach (var hit in Physics2D.RaycastAll(new Vector2(centre.x, bottom + 0.01f), Vector2.down, 100f))
		{
			if (hit.collider.isTrigger || !ObjectNames.IsSurface(hit.collider.gameObject.name)) continue;
			float drop = bottom - (hit.point.y + _motor.skin);
			if (drop > 0.01f) transform.position += Vector3.down * drop;
			return;
		}
	}

	// the nearest-to-the-camera z of an object's meshes (skinned ones as posed now)
	float FrontZ(GameObject go)
	{
		float z = float.MaxValue;
		foreach (var r in go.GetComponentsInChildren<Renderer>())
			z = Mathf.Min(z, r is SkinnedMeshRenderer smr ? PosedZ(smr, false) : r.bounds.min.z);
		return z;
	}

	// the deepest (or nearest) point of a skinned mesh as posed now: its renderer's bounds come from the bones and
	// miss poses such as lying down or a stunned eskimo's arms
	Mesh _posed;
	float PosedZ(SkinnedMeshRenderer smr, bool deepest)
	{
		if (!_posed) _posed = new Mesh();
		smr.BakeMesh(_posed, true);
		var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
		float z = deepest ? float.MinValue : float.MaxValue;
		foreach (var v in _posed.vertices)
		{
			float vz = m.MultiplyPoint3x4(v).z;
			z = deepest ? Mathf.Max(z, vz) : Mathf.Min(z, vz);
		}
		return z;
	}

	// the model moved towards the camera until it is all in front of the killer's model: it lies over the obstacle /
	// eskimo instead of inside it
	void InFrontOf(GameObject killer)
	{
		float killerFront = float.MaxValue, bearBack = float.MinValue;
		killerFront = FrontZ(killer);
		var enemy = killer.GetComponent<EnemyLogic>();                   // an eskimo's spear is not its child
		if (enemy && enemy.spear) killerFront = Mathf.Min(killerFront, FrontZ(enemy.spear.gameObject));
		foreach (var smr in _animator.GetComponentsInChildren<SkinnedMeshRenderer>()) bearBack = Mathf.Max(bearBack, PosedZ(smr, true));
		if (killerFront == float.MaxValue || bearBack == float.MinValue) return;
		float dz = killerFront - InFrontGap - bearBack;
		// (the model's parent: the clips drive the model's own transform)
		if (dz < 0f) _animator.transform.parent.position += Vector3.forward * dz;
	}

	// the weakness overused: sleep (roar) or cry (punch) for a while, no control - once the bear is on the ground
	void UpdatePenalty()
	{
		if (_penalty)
		{
			_penaltyTimer -= Time.deltaTime;
			if (_penaltyTimer < 0f)
				_penalty = false;
			return;
		}
		// (not in the air: it waits until the bear has landed)
		if (randomCriteriaCounter <= PenaltyAfterUses || !_motor.IsGrounded)
			return;
		_penaltyTimer = PenaltySeconds;
		_penalty = true;
		bool sleep = randomCriteria == PlayerAbility.Growl;
		if (sleep || randomCriteria == PlayerAbility.Punch)
		{
			AnimStates.Play(_animator, sleep ? AnimStates.Sleep : AnimStates.Cry);
			var clip = sleep ? soundSleep : soundCry;
			if (clip) _audio.PlayOneShot(clip, 1f);
		}
		if (_moodFx) _moodFx.Play(sleep, _penaltyTimer);          // Z letters / tears over the 3D bear
		randomCriteriaCounter = 0;
	}

	void Walk()
	{
		if (_input.Right)
			Steer(1f);
		else if (_input.Left)
			Steer(-1f);
		else
		{
			_horizontal = 0f;
			if (_motor.IsGrounded && !inAction)
				AnimStates.Play(_animator, AnimStates.Idle);
		}
	}

	void Steer(float direction)
	{
		_horizontal = direction;
		var s = transform.localScale;
		if (s.x * direction < 0f)                                // face the way it walks
			transform.localScale = new Vector3(-s.x, s.y, s.z);
		if (_motor.IsGrounded)
			AnimStates.Play(_animator, AnimStates.Run);
	}

	void Jump()
	{
		if (!_motor.IsGrounded || !_input.Jump)
			return;
		_velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
		PlayJumpClip();
		bool super = _currentAbility == PlayerAbility.SuperJump || _extraAbility == PlayerAbility.SuperJump;
		_audio.PlayOneShot(super && soundSuperJump ? soundSuperJump : soundJump, 1f);
	}

	// the Jump clip from its take-off, at the speed that makes its airborne part last as long as this jump's flight
	// (a super jump flies longer): the bear leaves and lands with the physics
	void PlayJumpClip()
	{
		float flight = 2f * _velocity.y / -gravity;
		if (jumpClipSeconds <= 0f || jumpLanding <= jumpTakeoff || flight <= 0f)
		{
			AnimStates.Play(_animator, AnimStates.Jump);
			return;
		}
		_animator.SetFloat(JumpSpeed, (jumpLanding - jumpTakeoff) * jumpClipSeconds / flight);
		_animator.Play(Animator.StringToHash(AnimStates.Jump), 0, jumpTakeoff);
	}

	static readonly int JumpSpeed = Animator.StringToHash(AnimStates.JumpSpeedParameter);

	// white streaks flying from the bear's front across the punch's reach
	void ShowPunchReach(float facing)
	{
		if (!punchFx) return;
		var ls = transform.lossyScale;
		Vector3 centre = transform.TransformPoint(_collider.offset);
		float halfWidth = NormalReach * Mathf.Abs(ls.x) * 0.5f, height = _collider.size.y * Mathf.Abs(ls.y);
		punchFx.transform.position = centre + new Vector3(facing * halfWidth, height * 0.1f, -1.5f);
		punchFx.transform.rotation = Quaternion.LookRotation(Vector3.right * facing);
		punchFx.Emit(16);
	}

	void Move()
	{
		float damping = _motor.IsGrounded ? groundDamping : inAirDamping;
		_velocity.x = Mathf.Lerp(_velocity.x, _horizontal * runSpeed, Time.fixedDeltaTime * damping);
		_velocity.y += gravity * Time.fixedDeltaTime;
		_motor.Move(_velocity * Time.fixedDeltaTime);
	}

	// ---------- abilities ----------

	// the running ability's timer, or Space (the mood's ability) / Ctrl (the pill's) / Enter (take a pill)
	void Act(float facing)
	{
		if (inAction)
		{
			_actionTimer -= Time.deltaTime;
			if (_actionTimer <= 0f)
			{
				inAction = false;
				if (_activeAbility == PlayerAbility.Punch)
					SetReach(NormalReach);
				_activeAbility = PlayerAbility.None;
			}
		}
		else if (_input.Action && _currentAbility != PlayerAbility.SuperJump)
			UseAbility(_currentAbility, facing);
		else if (_input.Secondary && _extraAbility != PlayerAbility.SuperJump)
			UseAbility(_extraAbility, facing);
		else if (_input.Consume && !hasExtraAbility)
			TakePill();
	}

	// super jump is not "used": it raises jumpHeight while it is on. A punch widens the collider while it runs.
	void UseAbility(PlayerAbility ability, float facing)
	{
		if (randomCriteria == ability)
			randomCriteriaCounter++;
		if (ability == PlayerAbility.Growl)
		{
			_actionTimer = RoarSeconds;
			inAction = true;
			_activeAbility = ability;
			_animator.Play(Animator.StringToHash(AnimStates.Roar), 0, roarClipStart);   // the roar pose comes with the sound
			SpawnRoar(facing);
			_audio.PlayOneShot(soundGrawl, 1f);
		}
		else if (ability == PlayerAbility.Punch)
		{
			_actionTimer = PunchSeconds;
			inAction = true;
			_activeAbility = ability;
			SetReach(PunchReach);
			AnimStates.Play(_animator, AnimStates.Punch);
			ShowPunchReach(facing);
			_audio.PlayOneShot(soundPunch, 1f);
		}
	}

	// the roar wave: its hit box starts at the bear, its picture at the head (GrowlBehavior)
	void SpawnRoar(float facing)
	{
		var roar = Instantiate(growlPrefab, new Vector3(transform.position.x, transform.position.y, 0f), Quaternion.identity);
		roar.name = ObjectNames.Growl;
		var growl = roar.GetComponent<GrowlBehavior>();
		growl.direction = facing;
		if (_moodFx) growl.source = _moodFx.head;
	}

	void SetReach(float width) => _collider.size = new Vector2(width, _collider.size.y);

	// a pill: the next mood's ability on Ctrl until the mood changes
	void TakePill()
	{
		if (numberOfPills <= 0)
			return;
		var next = (PlayerState)((int)_currentState % 3 + 1);    // Aggressive -> Hysteric -> Ecstatic -> Aggressive
		_extraAbility = AbilityOf(next);
		if (_extraAbility == PlayerAbility.SuperJump)
			jumpHeight += jumpBonus;
		_pillAvatar.texture = Avatar(next);
		_pillLabel.texture = CtrlLabel(_extraAbility);
		Debug.Log("Consumed a pill. Extra ability : " + _extraAbility);
		_extraAbilityTimer = _moodTimer;
		hasExtraAbility = true;
		numberOfPills--;
		_pillText.text = numberOfPills.ToString();
	}

	void UpdatePillAbility()
	{
		if (!hasExtraAbility)
			return;
		_extraAbilityTimer -= Time.deltaTime;
		if (_extraAbilityTimer > 0f)
			return;
		hasExtraAbility = false;
		_pillAvatar.texture = null;
		_pillLabel.texture = null;
	}

	// ---------- moods ----------

	void UpdateMood()
	{
		_moodTimer -= Time.deltaTime;
		_timerText.text = Mathf.FloorToInt(_moodTimer).ToString();
		if (_moodTimer <= 0f)
			NextMood();
	}

	// a random other mood for 5..9 s; the pill's ability ends with the old mood
	void NextMood()
	{
		if (_currentAbility == PlayerAbility.SuperJump || _extraAbility == PlayerAbility.SuperJump)
			jumpHeight -= jumpBonus;

		var old = _currentState;
		while (_currentState == old)
			_currentState = (PlayerState)Random.Range(1, 4);
		_currentAbility = AbilityOf(_currentState);
		_extraAbility = PlayerAbility.None;
		if (_currentAbility == PlayerAbility.SuperJump)
			jumpHeight += jumpBonus;

		_moodTimer = Random.Range(5, 10);
		_timerText.text = Mathf.FloorToInt(_moodTimer).ToString();
		_moodAvatar.texture = Avatar(_currentState);
		_moodLabel.texture = SpaceLabel(_currentAbility);
		Debug.Log("Changed state to : " + _currentState + " for " + _moodTimer + " seconds!");
	}

	static PlayerAbility AbilityOf(PlayerState s) => (PlayerAbility)s;

	Texture2D Avatar(PlayerState s) =>
		s == PlayerState.Aggressive ? textureAggressive : s == PlayerState.Hysteric ? textureHysteric :
		s == PlayerState.Ecstatic ? textureEcstatic : null;

	Texture2D SpaceLabel(PlayerAbility a) =>
		a == PlayerAbility.Punch ? textureTextPunchSpace : a == PlayerAbility.Growl ? textureTextGrawlSpace :
		a == PlayerAbility.SuperJump ? textureTextSuperJumpW : null;

	Texture2D CtrlLabel(PlayerAbility a) =>
		a == PlayerAbility.Punch ? textureTextPunchCtrl : a == PlayerAbility.Growl ? textureTextGrawlCtrl :
		a == PlayerAbility.SuperJump ? textureTextSuperJumpW : null;
}
