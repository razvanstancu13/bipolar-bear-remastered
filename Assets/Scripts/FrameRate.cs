using UnityEngine;

// Most of the game moves things a fixed step per frame (clouds, parallax, enemies, the growl, the balance timer),
// which was tuned for the 60 frames per second of 2014. On a faster screen it would all run faster, so the game is
// held at 60.
public static class FrameRate
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Lock()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
	}
}
