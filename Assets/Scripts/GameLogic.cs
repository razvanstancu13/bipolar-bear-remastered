using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The level's referee (on the GameManager object): a death plays the bear's death clip, then restarts the level.
public class GameLogic : MonoBehaviour
{
	// the bear's death clip plays this long (after its impact, PlayerController.ImpactSeconds) before the restart
	public const float DeathSeconds = 1.2f;

	public PlayerController Player;

	int _score;
	bool _dying;

	// killer: the obstacle or eskimo that killed the bear (it is put down on the ground in front of it), if any
	public void OnPlayerDead(GameObject killer = null)
	{
		if (_dying)
			return;
		_dying = true;
		var p = Player ? Player : FindAnyObjectByType<PlayerController>();
		if (p) p.Die(killer);
		StartCoroutine(Restart());
	}

	IEnumerator Restart()
	{
		yield return new WaitForSeconds(PlayerController.ImpactSeconds + DeathSeconds);
		SceneManager.LoadScene(GameScenes.Level);
	}

	public void OnCoinCollected() => _score++;
}
