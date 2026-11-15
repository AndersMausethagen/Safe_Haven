using System;
using System.Collections;
using UnityEngine;

// Story graph nodes in the "Camera & Sound" menu. Story nodes for the camera and sound: a shake, a camera focus,
// lightning, a sound and music. Each is small: settings, a Title and Category for the graph's Add menu, and Run (or
// Listen for a trigger). See StoryNode.

[Serializable]
public sealed class ShakeCameraNode : StoryNode
{
    [Range(0f, 1f)] public float strength = 0.6f;

    public override string Title => "Shake Camera";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        CameraShake.Add(strength);
        yield break;
    }
}

[Serializable]
public sealed class FocusCameraNode : StoryNode
{
    [Tooltip("What the camera turns to look at, for Seconds, before turning back to the player.")]
    public Transform target;
    [Min(0f)] public float seconds = 3f;
    [Tooltip("Lifts the dark (the night and unexplored ground) this many metres around it while the camera looks, so it can be seen. 0: off.")]
    [Min(0f)] public float revealRadius = 14f;
    [Tooltip("Turn even when the story is a fight (it has Spawn Enemies or Creature Hunts): the player keeps control, so it " +
             "suits a glance at what matters (the troll's fist coming down); the enemies wait and can't hurt the player meanwhile. Off: in a fight the camera stays with the player.")]
    public bool evenInAFight;

    public override string Title => "Focus Camera";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        // Around a fight the camera stays with the player.
        if (context.InFight && !evenInAFight)
        {
            yield break;
        }

        PlayerCameraController camera = PlayerCameraController.Current;
        if (camera == null || target == null)
        {
            yield break;
        }

        GameObject sight = null;
        if (revealRadius > 0f)
        {
            sight = new GameObject("Story Sight");
            sight.transform.SetParent(target, false);
            sight.AddComponent<VisibilitySource>().Configure(revealRadius, 0.4f, true, revealRadius, false);
        }

        // In a fight the camera looking away is no opening for the enemies (the user, 2026-11-15: "monsters still attack me
        // during a cinematic"): they start no move or blow and the player can't be hurt until it is back.
        if (context.InFight)
        {
            InGameCinematic.HoldTheWorldFor(seconds);
            Damageable health = context.Player != null ? context.Player.Health : null;
            if (health != null)
            {
                health.AddInvulnerability(this);
                camera.StartCoroutine(Unshield(health, this, seconds));
            }
        }

        camera.BeginFocus(target);
        yield return new WaitForSeconds(seconds);
        camera.EndFocus(target);
        if (sight != null)
        {
            UnityEngine.Object.Destroy(sight);
        }
    }

    // On the camera, so it ends even if the story is stopped while the camera looks away.
    private static IEnumerator Unshield(Damageable health, object source, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (health != null)
        {
            health.RemoveInvulnerability(source);
        }
    }

    public override string FindProblem() => target == null ? "choose what to look at" : null;
}

/// <summary>Plays an <see cref="InGameCinematic"/> placed in the scene: its shots take the camera (black bars, the player
/// held) while the story's other branches make things happen. Unlike Focus Camera it plays in a fight too.</summary>
[Serializable]
public sealed class PlayCinematicNode : StoryNode, IStoryWait
{
    [Tooltip("The In-Game Cinematic in the scene (GameObject › Safe Haven › In-Game Cinematic, or add the component).")]
    public InGameCinematic cinematic;
    [Tooltip("Carry on only once the camera is back with the player. Off: carry on at once, beside it.")]
    public bool waitUntilFinished = true;

    public float MostSeconds => (cinematic != null ? cinematic.Seconds : 0f) + 2f;

    public override string Title => "Play Cinematic";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        if (cinematic == null)
        {
            yield break;
        }

        cinematic.Play();
        float giveUpAt = Time.time + MostSeconds;
        while (waitUntilFinished && cinematic != null && cinematic.IsRunning && Time.time < giveUpAt)
        {
            yield return null;
        }
    }

    public override string FindProblem()
    {
        if (cinematic == null) return "choose the cinematic";
        foreach (string problem in cinematic.FindProblems()) return problem;
        return null;
    }
}

/// <summary>Moves a playing In-Game Cinematic on: to its next shot, or to a given one. With shots that Hold Until Cued, the
/// camera follows a conversation line by line (the morning introductions: whoever speaks is framed).</summary>
[Serializable]
public sealed class CueCinematicNode : StoryNode
{
    public InGameCinematic cinematic;
    [Tooltip("The shot to go to (1 is the first). 0: the next one.")]
    [Min(0)] public int shot;

    public override string Title => shot > 0 ? $"Cinematic Shot {shot}" : "Cinematic: Next Shot";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        if (cinematic != null)
        {
            cinematic.Cue(shot);
        }

        yield break;
    }

    public override string FindProblem() => cinematic == null ? "choose the cinematic" : shot > cinematic.Shots.Count ? $"it has only {cinematic.Shots.Count} shots" : null;
}

/// <summary>Ends a playing In-Game Cinematic, gliding back to the game's camera.</summary>
[Serializable]
public sealed class StopCinematicNode : StoryNode
{
    public InGameCinematic cinematic;

    public override string Title => "Stop Cinematic";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        if (cinematic != null)
        {
            cinematic.Finish();
        }

        yield break;
    }

    public override string FindProblem() => cinematic == null ? "choose the cinematic" : null;
}

[Serializable]
public sealed class LightningNode : StoryNode
{
    public override string Title => "Lightning";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        WeatherSystem system = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
        if (system != null)
        {
            system.Strike();
        }
        else
        {
            Debug.LogWarning("Lightning: the scene has no WeatherSystem (World Tools › Sky & Weather).", context.Script);
        }

        yield break;
    }
}

[Serializable]
public sealed class PlaySoundNode : StoryNode
{
    public SoundDefinition sound;
    [Tooltip("Where it is heard from. Empty: at the player.")]
    public Transform at;

    public override string Title => "Play Sound";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        Vector3 where = at != null ? at.position : context.Player != null ? context.Player.transform.position : Vector3.zero;
        AudioManager.Play(sound, where);
        yield break;
    }

    public override string FindProblem() => sound == null ? "choose the sound" : null;
}

[Serializable]
public sealed class MusicNode : StoryNode
{
    public enum Change { Play, Silence, Resume }

    public Change change = Change.Play;
    [Tooltip("For Play: the music, such as combat music.")]
    public MusicPlaylist playlist = new MusicPlaylist();
    [Tooltip("For Play: combat music takes over while the player fights, and this comes back after (a place's mood music).")]
    public bool fightsInterrupt;

    public override string Title => change == Change.Play ? "Play Music" : change == Change.Silence ? "Silence Music" : "Resume Music";
    public override string Category => "Camera & Sound";

    public override IEnumerator Run(StoryContext context)
    {
        SceneAudio audio = UnityEngine.Object.FindAnyObjectByType<SceneAudio>();
        if (audio == null)
        {
            yield break;
        }

        switch (change)
        {
            case Change.Play: audio.PlayEventMusic(playlist, fightsInterrupt); break;
            case Change.Silence: audio.SilenceMusic(); break;
            case Change.Resume: audio.ResumeMusic(); break;
        }
    }

    public override string FindProblem() => change == Change.Play && !playlist.HasTracks ? "add the music" : null;
}
