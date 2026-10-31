using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts the Editor back after a test run. The runner swaps the user's scenes back itself, but tests that start scenes of
/// their own in Single mode (TestScenes.StartClean) can leave it on an untitled scratch scene, dirty, so the next thing to
/// touch the scene asks "Save?" and blocks the Editor. This remembers the scene when the run starts and returns to it.
/// </summary>
[InitializeOnLoad]
public static class TestRunCleanup
{
    private static string sceneBefore;

    static TestRunCleanup()
    {
        var api = UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
        api.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        api.RegisterCallbacks(new Callbacks());
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            string path = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(path))
            {
                sceneBefore = path;
            }
        }

        // After the runner's own restore, which is what this is a fallback for.
        public void RunFinished(ITestResultAdaptor result) => EditorApplication.delayCall += Restore;

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }

    private static void Restore()
    {
        if (!string.IsNullOrEmpty(sceneBefore) && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.OpenScene(sceneBefore, OpenSceneMode.Single);
        }
    }
}
