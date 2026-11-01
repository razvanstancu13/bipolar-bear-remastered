using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Test run, only when the game is started with  -bear-test <folder> [walk|eskimo|roar|punch|relocate]  (nothing in
// normal play).
// Every mode: a screenshot of the menu and its music, then the level; logs every level (re)load and every error,
// ends with "BEARTEST done: mode=... errors=N" and quits (tools/test_run.ps1 reads these lines).
//   normal  the bear idles: its position, mood, pills, clip and feet (gap between soles and ground), then a pill's spin
//   walk    the bear holds "right": it must die at the first obstacle with the Death clip before the level restarts
//   eskimo  an eskimo is stunned (stars), then the bear is put in front of it: the spear thrust must kill the bear
//   roar    the bear roars: the wave picture must start at its head; a roar to the left must also disappear
//   punch   asleep / crying while walking the bear must not slide; a pill punch (Ctrl) must knock an eskimo away
//           without the bear dying, and the bear's reach must be back to normal afterwards
//   relocate  overused in mid-jump the bear must fall asleep only after landing; dropped on a hazard and on an eskimo it must die
//             straight below (same x), on the ground, its model in front of the killer's
public class GameTestRun : MonoBehaviour
{
	string _dir, _mode = "";
	int _loads, _errors;
	bool _holdRoar, _deathSeen, _pressLeft, _pressRight, _pillPunch, _pressJump;
	Vector3 _bearStart;

	string ModeName => _mode == "" ? "normal" : _mode;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void Create()
	{
		var args = System.Environment.GetCommandLineArgs();
		int i = System.Array.IndexOf(args, "-bear-test");
		if (i < 0 || i + 1 >= args.Length)
			return;
		var go = new GameObject("GameTestRun");
		DontDestroyOnLoad(go);
		var t = go.AddComponent<GameTestRun>();
		t._dir = args[i + 1];
		if (i + 2 < args.Length && !args[i + 2].StartsWith("-"))
			t._mode = args[i + 2];
	}

	void Awake()
	{
		float levelStart = 0f;
		Application.logMessageReceived += (msg, stack, type) =>
		{
			if (type != LogType.Error && type != LogType.Exception) return;
			_errors++;
			if (_errors <= 20) Log("error: " + msg + " | " + stack.Split('\n')[0]);
		};
		SceneManager.sceneLoaded += (s, m) =>
		{
			_loads++;
			Log("loaded " + s.name + " at " + Time.time.ToString("F1") + " s" +
			    (_loads > 2 ? " (after " + (Time.time - levelStart).ToString("F1") + " s in the level)" : ""));
			levelStart = Time.time;
			// the keys the mode presses, on the new level's bear
			var p = FindAnyObjectByType<PlayerController>();
			if (p) p.inputOverride = Press;
		};
	}

	// walk: "right" all the time; roar: Space with the roar as the mood's ability, until the wave is out
	PlayerInput Press(PlayerInput input)
	{
		if (_mode == "walk" || _pressRight)
			input.Right = true;
		if (_pressLeft)
			input.Left = true;
		if (_pressJump)
			input.Jump = true;
		if (_pillPunch)
		{
			var b = FindAnyObjectByType<PlayerController>();
			if (b) { b._extraAbility = PlayerAbility.Punch; b.hasExtraAbility = true; }
			input.Secondary = true;
		}
		if (_holdRoar)
		{
			var p = FindAnyObjectByType<PlayerController>();
			if (p) p._currentAbility = PlayerAbility.Growl;
			input.Action = true;
		}
		return input;
	}

	IEnumerator Start()
	{
		Directory.CreateDirectory(_dir);
		yield return new WaitForSeconds(1.5f);
		yield return Shot("1-menu");
		CheckMusic("menu");
		SceneManager.LoadScene(GameScenes.Level);
		if (_mode == "eskimo")
			yield return EskimoTest();
		else if (_mode == "roar")
			yield return RoarTest();
		else if (_mode == "punch")
			yield return PunchTest();
		else if (_mode == "relocate")
			yield return RelocateTest();
		else
			yield return LevelTest();
		Done();
	}

	void Done()
	{
		Log("done: mode=" + ModeName + " loads=" + _loads + " errors=" + _errors);
		Application.Quit();
	}

	// normal / walk: four looks at the bear over 10 s
	IEnumerator LevelTest()
	{
		if (_mode == "walk") StartCoroutine(WatchDeath());
		foreach (var t in new[] { 1.5f, 2f, 3f, 4f })
		{
			yield return new WaitForSeconds(t);
			var p = FindAnyObjectByType<PlayerController>();
			Log("t=" + Time.time.ToString("F1") + " scene=" + SceneManager.GetActiveScene().name +
			    " bear=" + (p ? p.transform.position.ToString("F1") + " state=" + p._currentState + " pills=" + p.numberOfPills +
			                    " clip=" + ClipOf(p) + " feet=" + FeetGap(p) : "none") +
			    " loads=" + _loads + " errors=" + _errors);
			yield return Shot("2-level-" + Time.time.ToString("F0"));
		}
		if (_mode == "walk" && !_deathSeen) Error("the bear died without the Death clip");
		CheckMusic("level");
		if (_mode == "")
		{
			CheckBearSounds();
			CheckEffects();
			yield return EffectShots();
			yield return BlizzardCheck();
			yield return JumpSyncCheck();
			yield return MoveFeelCheck();
			yield return WallCheck();
			yield return PillCheck();
			yield return HeadBumpCheck();
			yield return UnderPlatformShot();
			yield return EskimoWalkCheck();
			yield return SpinCheck();
		}
	}

	// walk: the first death must play the Death clip before the level restarts (two shots of it)
	IEnumerator WatchDeath()
	{
		while (!_deathSeen)
		{
			var p = FindAnyObjectByType<PlayerController>();
			if (p && ClipOf(p) == AnimStates.Death)
			{
				_deathSeen = true;
				int loads = _loads;
				Log("death clip=Death at " + Time.time.ToString("F1") + " s");
				yield return new WaitForSeconds(0.25f);
				yield return Shot("3-death-a");
				yield return new WaitForSeconds(0.45f);
				yield return Shot("3-death-b");
				Log("level restarted after the death clip: " + (_loads == loads ? "not yet (ok)" : "already"));
			}
			yield return null;
		}
	}

	// eskimo: the eskimo nearest the bear on the ground is stunned (stars), then the bear is put in front of it
	// (spear thrust; the strike should kill the bear and reload the level)
	IEnumerator EskimoTest()
	{
		yield return new WaitForSeconds(2f);
		var p = FindAnyObjectByType<PlayerController>();
		var e = p ? NearestEskimoOnTheGround(p) : null;
		if (!p || !e) { Error("no bear or no eskimo on the ground"); yield break; }
		Log("eskimo " + e.name + " at " + e.transform.position.ToString("F1"));

		Place(p, e.transform.position.x - 7f, e.transform.position.y);
		e.Stun();
		yield return new WaitForSeconds(0.5f);
		Log("stunned clip=" + ClipOf(e) + " stars=" + StarsOn(e));
		yield return Shot("2-stun-a");
		yield return new WaitForSeconds(0.7f);
		yield return Shot("2-stun-b");
		yield return new WaitForSeconds(2.5f);   // the stun ends after 3 s
		Log("after stun clip=" + ClipOf(e) + " stars=" + StarsOn(e));

		int loads = _loads;
		float face = e.transform.localScale.x > 0f ? -1f : 1f;   // the eskimo looks left while its scale.x > 0
		Place(p, e.transform.position.x + face * 2.2f, e.transform.position.y);
		for (int i = 0; i < 4 && _loads == loads; i++)
		{
			yield return new WaitForSeconds(0.12f);
			if (e) Log("thrust " + i + " clip=" + ClipOf(e));
			yield return Shot("3-thrust-" + i);
		}
		yield return new WaitForSeconds(2.2f);              // impact + death clip, then the restart
		Log("spear killed the bear: " + (_loads > loads ? "yes" : "no"));
		if (_loads == loads) _errors++;
	}

	static EnemyLogic NearestEskimoOnTheGround(PlayerController p)
	{
		EnemyLogic best = null;
		float bestDx = float.MaxValue;
		foreach (var e in FindObjectsByType<EnemyLogic>())
		{
			float dx = Mathf.Abs(e.transform.position.x - p.transform.position.x);
			if (Mathf.Abs(e.transform.position.y - p.transform.position.y) < 3f && dx < bestDx) { best = e; bestDx = dx; }
		}
		return best;
	}

	// roar: the sound wave (particles) must start at the bear's mouth (within half a bear height), then spread ahead
	IEnumerator RoarTest()
	{
		yield return new WaitForSeconds(2.5f);
		var p = FindAnyObjectByType<PlayerController>();
		var fx = p ? p.GetComponent<MoodFx>() : null;
		if (!p || !fx || !fx.head) { Error("no bear / no MoodFx head"); yield break; }
		Log("before roar feet=" + FeetGap(p));
		float bearH = p.GetComponentInChildren<SkinnedMeshRenderer>().bounds.size.y;
		// press until the wave exists, then measure on that first frame (it travels ~18 units / s, gone after 10)
		_holdRoar = true;
		float until = Time.time + 1f;
		while (!Wave() && Time.time < until) yield return null;
		_holdRoar = false;
		bool first = true;
		foreach (var t in new[] { 0f, 0.1f, 0.15f })
		{
			if (t > 0f) yield return new WaitForSeconds(t);
			var w = Wave();
			if (!w) { Error("no roar wave (RoarWave)"); yield break; }
			Vector3 h = fx.head.position, e = w.transform.position;
			var b = w.GetComponent<ParticleSystemRenderer>().bounds;
			float face = p.transform.localScale.x < 0 ? -1f : 1f;
			float dy = e.y - h.y, dx = (e.x - h.x) * face, ahead = ((face > 0 ? b.max.x : -b.min.x) - face * h.x);
			Log("roar clip=" + ClipOf(p) + " wave from " + e.ToString("F2") + " (" + w.particleCount + " arcs, reaching " +
			    ahead.ToString("F2") + " ahead) head " + h.ToString("F2") + " dy=" + dy.ToString("F2") + " start dx=" + dx.ToString("F2"));
			if (first && (Mathf.Abs(dy) > bearH * 0.5f || Mathf.Abs(dx) > bearH * 0.5f)) Error("the wave does not start at the head");
			first = false;
			yield return Shot("2-roar-" + t.ToString("F2"));
		}

		// a roar to the left must disappear after its distance too (it used to fly on for ever)
		yield return new WaitForSeconds(1.3f);             // the first roar's 1.2 s
		_pressLeft = true;
		yield return new WaitForSeconds(0.1f);
		_pressLeft = false;
		_holdRoar = true;
		until = Time.time + 1f;
		while (!Wave() && Time.time < until) yield return null;
		_holdRoar = false;
		var roar = GameObject.Find(ObjectNames.Growl);
		if (!roar) { Error("no roar to the left"); yield break; }
		float t0 = Time.time;
		while (roar && Time.time < t0 + 2f) yield return null;
		Log("roar to the left removed: " + (roar ? "NO (still there after 2 s)" : "after " + (Time.time - t0).ToString("F2") + " s"));
		if (roar) Error("the roar to the left does not disappear");
	}

	// relocate: asleep in mid-jump the bear falls to the ground; killed by landing on a hazard, and on an eskimo, it is
	// put straight down on the ground (same x), its model in front of the killer's
	IEnumerator RelocateTest()
	{
		yield return new WaitForSeconds(2.5f);
		var p = FindAnyObjectByType<PlayerController>();
		if (!p) { Error("no bear"); yield break; }
		p.randomCriteria = PlayerAbility.Growl;

		_pressJump = true;
		yield return new WaitForSeconds(0.1f);
		_pressJump = false;
		yield return new WaitForSeconds(0.2f);             // rising
		p.randomCriteriaCounter = 6;                       // overused in the air: the penalty must wait for the landing
		yield return new WaitForSeconds(0.1f);
		string air = ClipOf(p);
		Log("overused at y=" + p.transform.position.y.ToString("F2") + " (in the air): clip=" + air);
		if (air == AnimStates.Sleep) Error("fell asleep in the air");
		yield return new WaitForSeconds(1.2f);
		yield return Shot("2-asleep");
		string feet = FeetGap(p), landed = ClipOf(p);
		Log("after landing: clip=" + landed + " feet=" + feet);
		if (landed != AnimStates.Sleep) Error("did not fall asleep after landing");
		if (!OnTheGround(feet)) Error("asleep after a jump: not on the ground");
		yield return new WaitForSeconds(4.5f);             // the penalty's 5 s

		var hazard = NearestHazard(p);
		if (!hazard) { Error("no crystal / spikes / volcano"); yield break; }
		yield return DieOn(p, hazard.gameObject, hazard.bounds, "3-dead-hazard");

		// after the restart: onto an eskimo (stunned, so it stands still and does not thrust)
		int loads = _loads;
		while (_loads == loads) yield return null;
		yield return new WaitForSeconds(2f);
		p = FindAnyObjectByType<PlayerController>();
		var e = p ? NearestEskimoOnTheGround(p) : null;
		if (!e) { Error("no bear or no eskimo on the ground"); yield break; }
		e.Stun();
		yield return DieOn(p, e.gameObject, e.GetComponent<Collider2D>().bounds, "4-dead-eskimo");
	}

	// the bear dropped from above onto `killer` must die there: same x, on the ground, its model in front of the killer's
	IEnumerator DieOn(PlayerController p, GameObject killer, Bounds kb, string shot)
	{
		int loads = _loads;
		float x = kb.center.x;
		Log("dropping the bear on " + killer.name + " at x=" + x.ToString("F1"));
		Place(p, x, kb.max.y + 2.5f);
		float t0 = Time.time;
		bool impact = false;
		while (ClipOf(p) != AnimStates.Death && Time.time < t0 + 2f)
		{
			if (!impact && GameObject.Find("Impact")) { impact = true; yield return Shot(shot + "-impact"); }
			yield return null;
		}
		Log("impact shown before the death clip: " + (impact ? "yes" : "no"));
		if (!impact) Error("no impact before the death clip");
		if (ClipOf(p) != AnimStates.Death) { Error("dropped on " + killer.name + ", the bear did not die"); yield break; }
		yield return new WaitForSeconds(0.6f);
		yield return Shot(shot);
		if (_loads != loads) { Error("the level restarted before the check"); yield break; }
		string feet = FeetGap(p);
		float dx = p.transform.position.x - x;
		float bearBack = MaxZ(p.GetComponentInChildren<Animator>().gameObject), killerFront = killer ? MinZ(killer) : float.NaN;
		var spear = killer ? killer.GetComponent<EnemyLogic>() : null;
		if (spear && spear.spear) killerFront = Mathf.Min(killerFront, MinZ(spear.spear.gameObject));   // (its spear too)
		Log("dead on " + killer.name + ": moved x by " + dx.ToString("F2") + ", feet=" + feet + ", bear model back z=" +
		    bearBack.ToString("F2") + " vs its front z=" + killerFront.ToString("F2"));
		if (Mathf.Abs(dx) > 0.1f) Error("dead on " + killer.name + ": moved sideways");
		if (!OnTheGround(feet)) Error("dead on " + killer.name + ": not on the ground");
		if (!(bearBack < killerFront)) Error("dead on " + killer.name + ": not in front of it");
	}

	static bool OnTheGround(string feet) =>
		float.TryParse(feet, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out float gap) &&
		Mathf.Abs(gap) <= 0.1f;

	// the nearest-to-the-camera z of an object's meshes (skinned ones baked as posed now)
	static float MinZ(GameObject go)
	{
		float z = float.MaxValue;
		var m = new Mesh();
		foreach (var r in go.GetComponentsInChildren<Renderer>())
		{
			if (!(r is SkinnedMeshRenderer smr)) { z = Mathf.Min(z, r.bounds.min.z); continue; }
			smr.BakeMesh(m, true);
			var w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
			foreach (var v in m.vertices) z = Mathf.Min(z, w.MultiplyPoint3x4(v).z);
		}
		Destroy(m);
		return z;
	}

	// the deepest point of the (skinned) meshes as posed now: baked, since a skinned renderer's bounds miss the pose
	static float MaxZ(GameObject go)
	{
		float z = float.MinValue;
		var m = new Mesh();
		foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
		{
			smr.BakeMesh(m, true);
			var w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
			foreach (var v in m.vertices) z = Mathf.Max(z, w.MultiplyPoint3x4(v).z);
		}
		Destroy(m);
		return z;
	}

	// the first hazard ahead of the bear
	static Collider2D NearestHazard(PlayerController p)
	{
		Collider2D best = null;
		foreach (var c in FindObjectsByType<BoxCollider2D>())
			if (ObjectNames.IsHazard(c.gameObject.name) && c.transform.position.x > p.transform.position.x &&
			    (!best || c.transform.position.x < best.transform.position.x))
				best = c;
		return best;
	}

	// punch: no sliding while asleep / crying; a pill punch knocks an eskimo away and the bear survives
	IEnumerator PunchTest()
	{
		yield return new WaitForSeconds(2.5f);
		var p = FindAnyObjectByType<PlayerController>();
		if (!p) { Error("no bear"); yield break; }
		p.randomCriteria = PlayerAbility.Growl;          // the punches below must not put it to sleep

		// the penalty while walking right: it must stop where it is
		_pressRight = true;
		yield return new WaitForSeconds(0.5f);
		p.randomCriteriaCounter = 6;
		yield return new WaitForSeconds(0.1f);
		float x1 = p.transform.position.x;
		yield return new WaitForSeconds(1f);
		float slid = p.transform.position.x - x1;
		_pressRight = false;
		Log("asleep while walking: moved " + slid.ToString("F2") + " in 1 s, clip=" + ClipOf(p));
		if (Mathf.Abs(slid) > 0.1f) Error("the bear slides while asleep / crying");
		yield return new WaitForSeconds(4.5f);             // the penalty's 5 s

		// a pill punch into the nearest eskimo
		var e = NearestEskimoOnTheGround(p);
		if (!e) { Error("no eskimo on the ground"); yield break; }
		float side = Mathf.Sign(e.transform.position.x - p.transform.position.x);
		Place(p, e.transform.position.x - side * 1.6f, e.transform.position.y);
		int loads = _loads;
		_pillPunch = true;
		if (side > 0) _pressRight = true; else _pressLeft = true;
		float t0 = Time.time;
		string eskimoClip = null;
		int streaks = 0;
		while (e && !p.IsDead && Time.time < t0 + 2f)
		{
			if (p.punchFx) streaks = Mathf.Max(streaks, p.punchFx.particleCount);
			if (e.IsDead && eskimoClip == null)
			{
				_pillPunch = _pressRight = _pressLeft = false;   // stop: walking on would reach the next hazard
				yield return new WaitForSeconds(0.2f);
				if (e) eskimoClip = ClipOf(e);
			}
			yield return null;
		}
		Log("punched eskimo played: " + (eskimoClip ?? "nothing") + " before it disappeared; punch streaks: " + streaks);
		if (streaks == 0) Error("no punch streaks");
		if (eskimoClip != "Death") Error("the punched eskimo did not play its death clip");
		_pillPunch = _pressRight = _pressLeft = false;
		yield return Shot("2-punch");
		bool knocked = !e, survived = !p.IsDead && _loads == loads;
		Log("pill punch: eskimo " + (knocked ? "knocked away" : "still there") + ", bear " + (survived ? "alive" : "killed"));
		if (!knocked || !survived) Error("the pill punch did not win against the eskimo");
		yield return new WaitForSeconds(0.6f);
		float reach = p.GetComponent<BoxCollider2D>().size.x;
		Log("reach after the punch: " + reach.ToString("F2") + " (inAction=" + p.inAction + " punching=" + p.IsPunching + " clip=" + ClipOf(p) + ")");
		if (Mathf.Abs(reach - 1f) > 0.01f) Error("the bear's reach stays wide after a punch");
	}

	static ParticleSystem Wave()
	{
		var w = GameObject.Find("RoarWave");
		return w ? w.GetComponent<ParticleSystem>() : null;
	}

	// a pill spinning about the vertical keeps its height on screen; a shear (spinning inside an uneven scale) does not
	IEnumerator SpinCheck()
	{
		var s = FindAnyObjectByType<Spin>();
		var r = s ? s.GetComponentInChildren<Renderer>() : null;
		if (!r) { Error("no spinning pill"); yield break; }
		float lo = float.MaxValue, hi = 0f;
		for (int i = 0; i < 30; i++)
		{
			lo = Mathf.Min(lo, r.bounds.size.y); hi = Mathf.Max(hi, r.bounds.size.y);
			yield return new WaitForSeconds(0.05f);
		}
		float change = (hi - lo) / hi * 100f;
		Log("spin " + s.transform.parent.parent.name + " height " + lo.ToString("F2") + ".." + hi.ToString("F2") + " (" + change.ToString("F1") + " %)");
		if (change > 3f) Error("the spinning pill changes shape");
	}

	// every sound of the bear's must be one of the LAGP sounds (sfx_*), none missing
	void CheckBearSounds()
	{
		var p = FindAnyObjectByType<PlayerController>();
		if (!p) { Error("no bear"); return; }
		var sounds = new[] { ("jump", p.soundJump), ("super jump", p.soundSuperJump), ("punch", p.soundPunch),
		                     ("roar", p.soundGrawl), ("sleep", p.soundSleep), ("cry", p.soundCry), ("death", p.soundDeath) };
		string line = "";
		foreach (var (what, clip) in sounds)
		{
			line += " " + what + "=" + (clip ? clip.name : "NONE");
			if (!clip || !clip.name.StartsWith("sfx_")) Error("the bear's " + what + " sound is " + (clip ? clip.name : "missing"));
		}
		Log("bear sounds:" + line);
	}

	// the crystals glow and sparkle, the volcanoes smoke
	void CheckEffects()
	{
		int glows = FindObjectsByType<Glow>().Length, sparkles = 0, smokes = 0;
		foreach (var ps in FindObjectsByType<ParticleSystem>())
			if (ps.isPlaying && ps.particleCount > 0)
			{
				if (ps.name == "Sparkles") sparkles++;
				else if (ps.name == "Smoke") smokes++;
			}
		Log("effects: " + glows + " glowing crystals, " + sparkles + " sparkling, " + smokes + " smoking volcanoes");
		if (glows == 0 || sparkles == 0 || smokes == 0) Error("crystal glow / sparkles / volcano smoke missing");
	}

	// a look at a glowing crystal and a smoking volcano (the camera stops following the bear meanwhile)
	IEnumerator EffectShots()
	{
		var cam = Camera.main;
		var follow = cam ? cam.GetComponent<CameraFollow>() : null;
		if (!follow) yield break;
		follow.enabled = false;
		var crystal = FindAnyObjectByType<Glow>();
		ParticleSystem smoke = null;
		foreach (var ps in FindObjectsByType<ParticleSystem>()) if (ps.name == "Smoke") { smoke = ps; break; }
		var at = new (string, Transform)[] { ("2-crystal", crystal ? crystal.transform : null), ("2-volcano", smoke ? smoke.transform : null) };
		foreach (var (shot, t) in at)
		{
			if (!t) continue;
			var c = cam.transform.position;
			cam.transform.position = new Vector3(t.position.x, t.position.y + 2f, c.z);
			yield return new WaitForSeconds(0.3f);
			yield return Shot(shot);
		}
		follow.enabled = true;
	}

	// the bear's box as it is now (world): its collider's bounds are of no use, BearMotor keeps it disabled
	static Rect BearBox(PlayerController p)
	{
		var c = p.GetComponent<BoxCollider2D>();
		Vector2 centre = p.transform.TransformPoint(c.offset);
		var s = p.transform.lossyScale;
		var size = new Vector2(c.size.x * Mathf.Abs(s.x), c.size.y * Mathf.Abs(s.y));
		return new Rect(centre - size * 0.5f, size);
	}

	// patrolling eskimos walk at the speed the game is tuned to (walkSpeed; some read slightly lower after a physics step)
	IEnumerator EskimoWalkCheck()
	{
		const float Tuned = 5.16f, Tolerance = 0.1f;
		yield return new WaitForFixedUpdate();
		int walking = 0;
		float slowest = float.MaxValue, fastest = 0f;
		foreach (var e in FindObjectsByType<EnemyLogic>())
		{
			float speed = Mathf.Abs(e.GetComponent<Rigidbody2D>().linearVelocity.x);
			if (speed < 0.1f) continue;                  // stunned or thrusting
			walking++;
			slowest = Mathf.Min(slowest, speed);
			fastest = Mathf.Max(fastest, speed);
		}
		Log("eskimo walk: " + walking + " walking at " + slowest.ToString("F3") + ".." + fastest.ToString("F3"));
		if (walking == 0 || Mathf.Abs(slowest - Tuned) > Tolerance || Mathf.Abs(fastest - Tuned) > Tolerance)
			Error("the eskimos do not walk at " + Tuned);
	}

	// the bear's speed, step by step: starting and stopping on the ground, steering in the air; and how high it jumps
	IEnumerator MoveFeelCheck()
	{
		var p = FindAnyObjectByType<PlayerController>();
		var motor = p.GetComponent<BearMotor>();
		_bearStart = p.transform.position;
		yield return new WaitForSeconds(0.2f);

		string Speeds(System.Collections.Generic.List<float> v) => string.Join(" ", v.ConvertAll(s => s.ToString("F3")));
		IEnumerator Record(System.Collections.Generic.List<float> into, int steps)
		{
			for (int i = 0; i < steps; i++)
			{
				yield return new WaitForFixedUpdate();
				into.Add(motor.Velocity.x);
			}
		}

		var starting = new System.Collections.Generic.List<float>();
		var stopping = new System.Collections.Generic.List<float>();
		var steering = new System.Collections.Generic.List<float>();
		_pressRight = true;
		yield return Record(starting, 8);
		_pressRight = false;
		yield return Record(stopping, 8);
		Log("feel: starting " + Speeds(starting));
		Log("feel: stopping " + Speeds(stopping));

		// a jump straight up, measured to its top, then steering in the air on the way down
		yield return new WaitForSeconds(0.2f);
		float height = p.jumpHeight, ground = p.transform.position.y, top = ground;
		_pressJump = true;
		yield return null;
		yield return new WaitForFixedUpdate();
		_pressJump = false;
		while (motor.Velocity.y > 0f)
		{
			top = Mathf.Max(top, p.transform.position.y);
			yield return new WaitForFixedUpdate();
		}
		top = Mathf.Max(top, p.transform.position.y);
		_pressRight = true;
		yield return Record(steering, 8);
		_pressRight = false;
		Log("feel: steering in the air " + Speeds(steering));
		Log("feel: jump height " + height.ToString("F1") + " rose " + (top - ground).ToString("F3"));
		// (the physics steps lose a little: half a step's rise)
		if (Mathf.Abs(top - ground - height) > 0.3f) Error("the jump's height is off");
		// the gap to the wanted speed shrinks by this share each step (as tuned: 40 % on the ground, 10 % in the air)
		void Rate(string what, System.Collections.Generic.List<float> v, float target, float expected)
		{
			float last = target - v[v.Count - 1], before = target - v[v.Count - 2], rate = last / before;
			if (Mathf.Abs(rate - expected) > 0.01f) Error(what + " changes speed at the wrong rate: " + rate.ToString("F3"));
		}
		Rate("starting", starting, p.runSpeed, 0.6f);
		Rate("stopping", stopping, 0f, 0.6f);
		Rate("steering", steering, p.runSpeed, 0.9f);
		yield return new WaitForSeconds(1.5f);
		yield return BackToStart(p);
	}

	// walking left from the start the bear must stop against the igloo's wall (the level's end)
	IEnumerator WallCheck()
	{
		var p = FindAnyObjectByType<PlayerController>();
		_pressLeft = true;
		yield return new WaitForSeconds(2f);
		_pressLeft = false;
		// the wall's real face at the bear's height (the igloo is slightly rotated, its bounds stick out further)
		var box = BearBox(p);
		var face = Physics2D.Raycast(box.center, Vector2.left, 20f, p.GetComponent<BearMotor>().solidLayers);
		if (!face.collider || !face.collider.name.StartsWith("iglu")) { Error("no igloo wall left of the bear"); yield break; }
		float gap = box.xMin - face.point.x;
		Log("wall: the bear's left edge is " + gap.ToString("F3") + " from the igloo's wall");
		if (Mathf.Abs(gap) > 0.05f) Error("the bear does not stop at the wall");
	}

	// walking into a pill must collect it
	IEnumerator PillCheck()
	{
		var p = FindAnyObjectByType<PlayerController>();
		Collider2D pill = null;
		foreach (var c in FindObjectsByType<Collider2D>())
			if (c.name == ObjectNames.Pill && (!pill || Mathf.Abs(c.transform.position.x - p.transform.position.x) <
			                                            Mathf.Abs(pill.transform.position.x - p.transform.position.x)))
				pill = c;
		if (!pill) { Error("no pill"); yield break; }
		int before = p.numberOfPills;
		Place(p, pill.transform.position.x, pill.transform.position.y);
		yield return new WaitForSeconds(0.15f);
		Log("pill: " + before + " -> " + p.numberOfPills + (pill ? ", still there" : ", gone"));
		if (p.numberOfPills != before + 1 || pill) Error("the pill was not collected");
		yield return BackToStart(p);
	}

	// the following checks start from the level's start, standing on the ground
	IEnumerator BackToStart(PlayerController p)
	{
		Place(p, _bearStart.x, _bearStart.y);
		yield return new WaitForSeconds(0.3f);
	}

	// jumping up under a platform the bear's head must stop at the platform's underside, then it falls back
	IEnumerator HeadBumpCheck()
	{
		var p = FindAnyObjectByType<PlayerController>();
		// a platform within a jump of the ground, with nothing dangerous (or anything else) around under it
		float standY = p.transform.position.y, feetToBear = standY - BearBox(p).yMin, bearHeight = BearBox(p).height;
		BoxCollider2D plat = null;
		float platStandY = 0f;
		foreach (var c in FindObjectsByType<BoxCollider2D>())
		{
			if (!c.name.StartsWith("platform") || c.isTrigger) continue;
			var ground = Physics2D.Raycast(new Vector2(c.bounds.center.x, c.bounds.min.y - 0.1f), Vector2.down, 30f,
			                               p.GetComponent<BearMotor>().solidLayers);
			if (!ground.collider || !ObjectNames.IsSurface(ground.collider.name)) continue;
			float room = c.bounds.min.y - ground.point.y - bearHeight;
			if (room < 0.5f || room > p.jumpHeight * 0.8f) continue;
			var column = new Vector2(c.size.x + 6f, c.bounds.min.y - ground.point.y - 0.2f);
			if (Physics2D.OverlapBox(new Vector2(c.bounds.center.x, ground.point.y + 0.1f + column.y * 0.5f), column, 0f)) continue;
			if (!plat || Mathf.Abs(c.bounds.center.x) < Mathf.Abs(plat.bounds.center.x))
			{
				plat = c;
				platStandY = ground.point.y + feetToBear;
			}
		}
		if (!plat) { Error("no clear platform within a jump (" + p.jumpHeight.ToString("F1") + ") of the ground"); yield break; }
		Place(p, plat.bounds.center.x, platStandY);
		yield return new WaitForSeconds(0.3f);
		_pressJump = true;
		yield return new WaitForSeconds(0.1f);
		_pressJump = false;
		float highest = float.MinValue;
		for (float t = 0f; t < 0.7f; t += Time.deltaTime)
		{
			highest = Mathf.Max(highest, BearBox(p).yMax);
			yield return null;
		}
		float gap = plat.bounds.min.y - highest;
		Log("head bump: the bear's head stopped " + gap.ToString("F3") + " under " + plat.name);
		if (Mathf.Abs(gap) > 0.05f) Error("the bear's head does not stop at the platform");
		yield return new WaitForSeconds(0.5f);   // it falls back down
		yield return BackToStart(p);
	}

	// a blizzard on demand: snow streaks must fill the view
	IEnumerator BlizzardCheck()
	{
		var b = FindAnyObjectByType<Blizzard>();
		if (!b) { Error("no blizzard"); yield break; }
		// never from below: the streaks always fly sideways or downwards
		float worstUp = -1f;
		for (int i = 0; i < 50; i++)
		{
			b.StartNow();
			worstUp = Mathf.Max(worstUp, (b.snow.transform.localRotation * Vector3.forward).y);
		}
		Log("blizzard directions: the most upward of 50 is " + worstUp.ToString("F2") + " (0 = sideways, 1 = straight up)");
		if (worstUp > 0.001f) Error("a blizzard came from below");
		b.StartNow();
		yield return new WaitForSeconds(1.8f);
		yield return Shot("2-blizzard");
		Log("blizzard: " + b.snow.particleCount + " streaks");
		if (b.snow.particleCount < 50) Error("the blizzard shows no snow");
	}

	// at the top of a jump the Jump clip must be halfway through its airborne part (it starts at take-off and its
	// flight is fitted to the jump's)
	IEnumerator JumpSyncCheck()
	{
		var p = FindAnyObjectByType<PlayerController>();
		var anim = p ? p.GetComponentInChildren<Animator>() : null;
		if (!anim || p.jumpClipSeconds <= 0f) { Error("no bear / no jump timing"); yield break; }
		float v = Mathf.Sqrt(2f * p.jumpHeight * -p.gravity), half = v / -p.gravity;
		_pressJump = true;
		yield return null;
		yield return new WaitForFixedUpdate();
		_pressJump = false;
		float start = anim.GetCurrentAnimatorStateInfo(0).normalizedTime;
		yield return new WaitForSeconds(half);
		float at = anim.GetCurrentAnimatorStateInfo(0).normalizedTime, mid = (p.jumpTakeoff + p.jumpLanding) * 0.5f;
		Log("jump: clip=" + ClipOf(p) + " started at " + start.ToString("F2") + " (take-off " + p.jumpTakeoff.ToString("F2") +
		    "), at the top " + at.ToString("F2") + " (airborne middle " + mid.ToString("F2") + ")");
		if (ClipOf(p) != "Jump" || Mathf.Abs(at - mid) > 0.08f) Error("the jump clip is out of step with the jump");
		yield return new WaitForSeconds(half + 0.3f);
	}

	// the bear and an eskimo put half inside a platform (as when jumping up through one): a screenshot - they must
	// show in front
	IEnumerator UnderPlatformShot()
	{
		var p = FindAnyObjectByType<PlayerController>();
		BoxCollider2D plat = null;
		foreach (var c in FindObjectsByType<BoxCollider2D>())
			if (c.gameObject.name.StartsWith("platform") && (!plat || Mathf.Abs(c.transform.position.x - p.transform.position.x) <
			                                                     Mathf.Abs(plat.transform.position.x - p.transform.position.x)))
				plat = c;
		if (!plat) { Error("no platform"); yield break; }
		Place(p, plat.bounds.center.x, plat.bounds.min.y - 0.3f);
		var e = NearestEskimoOnTheGround(p) ?? FindAnyObjectByType<EnemyLogic>();
		if (e) e.transform.position = new Vector3(plat.bounds.center.x + 2f, plat.bounds.min.y - 0.6f, e.transform.position.z);
		yield return Shot("2-under-platform");
		Log("bear under platform " + plat.name + " at x=" + plat.bounds.center.x.ToString("F1"));
		yield return BackToStart(p);   // out of that eskimo's reach: its spear would kill the bear and restart the level
	}

	// a looping music source must be playing (menu: music_menu, level: music_game)
	void CheckMusic(string where)
	{
		string playing = null;
		foreach (var s in FindObjectsByType<AudioSource>())
			if (s.isPlaying && s.loop && s.clip && s.clip.name.StartsWith("music")) playing = s.clip.name;
		Log(where + " music=" + (playing ?? "NONE"));
		if (playing == null) Error("no music in the " + where);
	}

	// soles (lowest point of the skinned mesh as posed now) minus the top of the ground / platform under the bear
	// (Make3D puts the slabs' visible top edge at or above it everywhere)
	static string FeetGap(PlayerController p)
	{
		var smr = p.GetComponentInChildren<SkinnedMeshRenderer>();
		if (!smr) return "no-mesh";
		var m = new Mesh();
		smr.BakeMesh(m, true);
		var w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
		float low = float.MaxValue;
		foreach (var v in m.vertices) low = Mathf.Min(low, w.MultiplyPoint3x4(v).y);
		Destroy(m);
		foreach (var hit in Physics2D.RaycastAll(p.transform.position, Vector2.down, 20f))
			if (!hit.collider.isTrigger && ObjectNames.IsSurface(hit.collider.gameObject.name))
				return (low - hit.point.y).ToString("F2");
		return "no-ground";
	}

	// moves the bear (and the camera with it)
	static void Place(PlayerController p, float x, float y)
	{
		p.transform.position = new Vector3(x, y, p.transform.position.z);
		var c = Camera.main;
		if (c) c.transform.position = new Vector3(x, c.transform.position.y, c.transform.position.z);
	}

	// the clip a 3D model is playing (NONE = no Animator or no clip: the model would stand frozen)
	static string ClipOf(Component o)
	{
		var a = o.GetComponentInChildren<Animator>();
		if (!a) return "NONE";
		var c = a.GetCurrentAnimatorClipInfo(0);
		return c.Length > 0 && c[0].clip ? c[0].clip.name : "NONE";
	}

	static bool StarsOn(EnemyLogic e)
	{
		foreach (var s in FindObjectsByType<StunStars>(FindObjectsInactive.Include))
			if (s.transform.IsChildOf(e.transform) || (s.head && s.head.IsChildOf(e.transform)))
				return s.gameObject.activeInHierarchy;
		return false;
	}

	static void Log(string line) => Debug.Log("BEARTEST " + line);

	void Error(string what)
	{
		Log("error: " + what);
		_errors++;
	}

	IEnumerator Shot(string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(_dir, (_mode == "" ? "" : _mode + "-") + name + ".png"));
		yield return null;
	}
}
