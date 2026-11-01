using UnityEngine;

// Names the code and the scenes / Make3D have to agree on, in one place.

// the two scenes, in build order (GameBuild sets that order; the indices are what the scripts load)
public static class GameScenes
{
	public const int Menu = 0;
	public const int Level = 1;
	public static readonly string[] Paths = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/GameScene.unity" };
}

// the layer of everything the character camera draws over the level: the bear's and the eskimos' models, the
// spears and stun stars, and the effects that go with them (impact, roar wave, punch streaks, Z's and tears)
public static class GameLayers
{
	public const int Characters = 12;          // ProjectSettings/TagManager: "Characters"
}

// Animator states the scripts play (Make3D builds controllers with exactly these states)
public static class AnimStates
{
	public const string Idle = "Idle", Run = "Run", Jump = "Jump", Punch = "Punch", Roar = "Grawl", Sleep = "Sleep",
	                    Cry = "Cry", Death = "Death";
	public const string EskimoWalk = "EskimoWalk", EskimoStunned = "EskimoIdle", EskimoDizzy = "Dizzy",
	                    EskimoAttack = "EskimoAttack", EskimoDeath = "EskimoDeath";

	public const string JumpSpeedParameter = "JumpSpeed";     // the Jump state's speed multiplier (PlayerController)

	public static void Play(Animator animator, string state) => animator.Play(Animator.StringToHash(state));
}

// object names the collision code recognises (as named in the scenes and prefabs)
public static class ObjectNames
{
	public const string Enemy = "Enemy", Coin = "Coin", Pill = "Pill", Growl = "Growl", GameManager = "GameManager";

	// what the bear can stand on: the ground pieces and the platforms (not the igloos: their tall boxes are the
	// level's end walls)
	public static bool IsSurface(string name) =>
		name.StartsWith("ground", System.StringComparison.OrdinalIgnoreCase) ||
		name.StartsWith("platform", System.StringComparison.OrdinalIgnoreCase);

	// the obstacles that kill the bear on touch
	public static bool IsHazard(string name) =>
		name == "props_cristal" || name == "props_cristal2" || name == "props_cristal3" || name == "props_tepi" ||
		name == "props_vulcan";
}
