using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// The results screen's painting alive (the user, 2026-11-13: "animate them ... simple movement and sound, trees swaying,
/// eyes blinking"): the raid's motion loop (RaidDefinition.ResultMotion, a LocalSpawn video that ends on its first frame)
/// fades in over the still once it plays, and the place's sound (ResultSound) loops under the report on the ambience
/// channel. The report's panel is see-through so more of the painting shows ("make it transparent ... but keep the
/// icons fully visible"): the haul's cards and the button stay solid.
/// </summary>
public sealed partial class RaidResultsScreen
{
    private const float MotionFadeSeconds = 0.8f;
    private const float SoundFadeSeconds = 2.5f;
    // How much of the panel's leather is left over the painting.
    private const float PanelAlpha = 0.55f;

    private VideoPlayer motionPlayer;
    private RawImage motionImage;
    private RenderTexture motionTexture;
    private AudioSource placeSound;

    private void PlayScene(RaidDefinition raid, bool home)
    {
        VideoClip clip = raid != null ? raid.ResultMotion(home) : null;
        if (clip != null && artwork != null)
        {
            PlayMotion(clip);
        }

        AudioClip sound = raid != null ? raid.ResultSound(home) : null;
        if (sound != null)
        {
            if (placeSound == null)
            {
                placeSound = gameObject.AddComponent<AudioSource>();
                placeSound.playOnAwake = false;
                placeSound.loop = true;
                placeSound.spatialBlend = 0f;
            }

            placeSound.clip = sound;
            placeSound.volume = 0f;
            placeSound.Play();
        }
    }

    // Over the still, in its rect (the artwork's fitter crops both the same), clear until the first frame is drawn.
    private void PlayMotion(VideoClip clip)
    {
        if (motionPlayer == null)
        {
            motionImage = CodeUI.Stretch("Motion", artwork.transform).gameObject.AddComponent<RawImage>();
            motionImage.raycastTarget = false;
            motionPlayer = gameObject.AddComponent<VideoPlayer>();
            motionPlayer.playOnAwake = false;
            motionPlayer.isLooping = true;
            motionPlayer.renderMode = VideoRenderMode.RenderTexture;
            motionPlayer.audioOutputMode = VideoAudioOutputMode.None;
            motionPlayer.skipOnDrop = true;
            motionPlayer.prepareCompleted += player => player.Play();
        }

        ReleaseMotion();
        motionTexture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "Result Motion" };
        motionPlayer.targetTexture = motionTexture;
        motionImage.texture = motionTexture;
        motionImage.color = new Color(1f, 1f, 1f, 0f);
        motionPlayer.clip = clip;
        motionPlayer.Prepare();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (motionImage != null && motionPlayer.isPlaying && motionPlayer.frame > 0 && motionImage.color.a < 1f)
        {
            motionImage.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(motionImage.color.a, 1f, dt / MotionFadeSeconds));
        }

        if (placeSound != null && placeSound.isPlaying)
        {
            float target = AudioManager.ChannelVolume(AudioChannel.Ambience);
            placeSound.volume = Mathf.MoveTowards(placeSound.volume, target, dt / SoundFadeSeconds);
        }
    }

    // Once the window's dress is on (a frame after it starts): its colour and its leather made see-through.
    private void SeeThrough()
    {
        var face = content.GetComponent<Image>();
        if (face != null)
        {
            face.color = Faded(face.color, PanelAlpha);
        }

        Transform ground = content.transform.Find("Hearthline Ground");
        if (ground != null)
        {
            foreach (Image layer in ground.GetComponentsInChildren<Image>(true))
            {
                layer.color = Faded(layer.color, PanelAlpha);
            }
        }
    }

    private static Color Faded(Color colour, float by) => new Color(colour.r, colour.g, colour.b, colour.a * by);

    private void ReleaseMotion()
    {
        if (motionTexture == null)
        {
            return;
        }

        if (motionPlayer != null)
        {
            motionPlayer.targetTexture = null;
        }

        motionTexture.Release();
        Destroy(motionTexture);
        motionTexture = null;
    }
}
