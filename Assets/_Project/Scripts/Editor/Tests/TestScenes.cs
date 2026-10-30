using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit Mode tests run in an untitled scratch scene the test runner makes, and swaps back for the user's scenes
/// afterwards; the tests before have usually added to it. Unity won't add a scene beside an untitled one, so a test that
/// needs a scene of its own works in a fresh one from <see cref="StartClean"/>, and calls it again when done so what it
/// made is gone before the next test.
/// </summary>
public static class TestScenes
{
    public static Scene StartClean() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
}
