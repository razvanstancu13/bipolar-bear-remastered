using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the game: menu "Bipolar Bear > Build Windows", or -executeMethod GameBuild.Windows. Output: Builds/Windows.
public static class GameBuild
{
	const string Output = "Builds/Windows/BipolarBear.exe";

	[MenuItem("Bipolar Bear/Build Windows")]
	public static void Windows()
	{
		// the scripts load scenes by index (GameScenes.Menu / Level), so this order matters
		EditorBuildSettings.scenes = GameScenes.Paths.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
		var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
		{
			scenes = GameScenes.Paths,
			locationPathName = Output,
			target = BuildTarget.StandaloneWindows64,
		});
		Debug.Log("BB build: " + r.summary.result + ", " + r.summary.totalErrors + " errors, " + (r.summary.totalSize / 1048576f).ToString("F1") + " MB");
		if (Application.isBatchMode)
			EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
	}
}
