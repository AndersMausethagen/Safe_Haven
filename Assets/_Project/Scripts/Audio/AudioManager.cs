using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the game's audio. GameSystems creates it when the game starts (with Audio Settings from a Resources folder)
/// and it lives across scenes, so no scene needs any setup. It:
/// <list type="bullet">
/// <item>picks music and ambience from the scene's SceneAudio (exploring, combat, raid final stretch) and falls back to the
/// default music in Audio Settings (see <see cref="MusicRules"/>), crossfading when that changes;</item>
/// <item>plays a sound whenever anything dies, reacting to the Killed and Destroyed game events: the object's
/// DeathSound, else its monster or building data's death sound, else the default for creatures or structures;</item>
/// <item>plays one-off sounds for anyone who asks through <see cref="Play"/>, from a pool of audio sources;</item>
/// <item>keeps the player's volume per channel (PlayerPrefs, not the save file, since it is a preference);</item>
/// <item>holds the game's ears: at the player, turned with the camera. On the camera, far above in the top-down view, a
/// sound at the player's feet would be heard from twenty metres off, and footsteps not at all.</item>
/// </list>
/// <para>
/// While the game is paused the world's sounds hold their breath (AudioListener.pause); music, ambience and the interface
/// go on, as they ignore it.
/// </para>
/// <para>
/// Extending: to make another kind of game event audible (quest completed, item obtained), handle its kind in
/// OnGameEvent and give Audio Settings a SoundDefinition field for it. To change what counts as combat, edit
/// UpdateMood and its combat cues.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour, IGameSystem
{
    private const float CombatCheckInterval = 0.5f;
    private const string VolumeKey = "SafeHaven.Volume.";
    private const float EarHeight = 1.6f;

    // Two sources per layer, so one fades out while the next fades in. Plays a playlist: one track loops, several take turns.
    private sealed class LoopLayer
    {
        private const float MinSecondsBeforeAdvance = 1f;
        private const float ResumeWithinSeconds = 120f;

        private readonly MusicMemory memory = new MusicMemory();
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly MusicTrack[] tracks = new MusicTrack[2];
        private readonly float[] fades = new float[2];
        private readonly float[] targets = new float[2];
        private readonly AudioChannel channel;
        private int current;
        private int index = -1;
        private float startedAt;

        public LoopLayer(GameObject owner, AudioChannel channel)
        {
            this.channel = channel;
            for (int i = 0; i < 2; i++)
            {
                sources[i] = owner.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].ignoreListenerPause = true;
            }
        }

        public MusicPlaylist Playlist { get; private set; }
        public MusicTrack Track => targets[current] > 0f ? tracks[current] : null;
        public AudioSource Source => sources[current];

        /// <summary>A new scene: its music has nothing to do with where the last scene's was.</summary>
        public void Forget() => memory.Clear();

        public void Switch(MusicPlaylist playlist)
        {
            if (playlist == Playlist)
            {
                return;
            }

            if (Playlist != null && index >= 0 && sources[current].isPlaying)
            {
                memory.Leave(Playlist, index, sources[current].time, Time.unscaledTime);
            }

            Playlist = playlist;
            if (memory.TryReturn(playlist, Time.unscaledTime, ResumeWithinSeconds, out int place, out float time) && place < playlist.tracks.Count)
            {
                targets[current] = 0f;
                index = place;
                StartTrack(time);
                return;
            }

            index = -1;
            PlayNext();
        }

        /// <summary>Moves on to the playlist's next track now.</summary>
        public void PlayNext()
        {
            targets[current] = 0f;
            index = Playlist != null ? Playlist.NextIndex(index) : -1;
            if (index >= 0)
            {
                StartTrack(0f);
            }
        }

        public void Restart()
        {
            if (Playlist == null || index < 0)
            {
                return;
            }

            targets[current] = 0f;
            StartTrack(0f);
        }

        private void StartTrack(float from)
        {
            MusicTrack track = Playlist.tracks[index];
            current = 1 - current;
            AudioSource next = sources[current];
            tracks[current] = track;
            fades[current] = 0f;
            targets[current] = 1f;
            next.clip = track.clip;
            next.loop = Playlist.PlayableCount == 1;
            next.pitch = 1f;
            next.volume = 0f;
            next.Play();
            if (from > 0f && from < track.clip.length)
            {
                next.time = from;
            }

            startedAt = Time.unscaledTime;
        }

        /// <param name="fadeStep">How far a fade moves this frame, as a share of the whole fade.</param>
        public void Update(float fadeStep)
        {
            AudioSource active = sources[current];
            if (Playlist != null && targets[current] > 0f && !active.loop && !active.isPlaying
                && Time.unscaledTime - startedAt > MinSecondsBeforeAdvance)
            {
                PlayNext();
            }

            for (int i = 0; i < 2; i++)
            {
                AudioSource source = sources[i];
                if (!source.isPlaying || tracks[i] == null)
                {
                    continue;
                }

                fades[i] = Mathf.MoveTowards(fades[i], targets[i], fadeStep);
                source.volume = EqualPower(fades[i]) * tracks[i].volume * ChannelVolume(channel);
                if (targets[i] <= 0f && fades[i] <= 0f)
                {
                    source.Stop();
                    tracks[i] = null;
                }
            }
        }
    }
    private static AudioManager instance;
    private static readonly Dictionary<AudioChannel, float> volumes = new Dictionary<AudioChannel, float>();
    private static float masterVolume = 1f;

    private readonly List<AudioSource> pool = new List<AudioSource>();
    private readonly Dictionary<AudioSource, SoundDefinition> playing = new Dictionary<AudioSource, SoundDefinition>();
    private readonly Dictionary<SoundDefinition, float> lastPlayedAt = new Dictionary<SoundDefinition, float>();
    private readonly Dictionary<SoundDefinition, AudioClip> lastClip = new Dictionary<SoundDefinition, AudioClip>();
    private AudioSettingsDefinition settings;
    private AudioListener ears;
    private LoopLayer music;
    private LoopLayer ambience;
    private WorldAudio world;
    private SceneAudio sceneAudio;
    private Damageable player;
    private float lastDamageTime = float.NegativeInfinity;
    private float combatUntil = float.NegativeInfinity;
    private float lastCombatEndedAt = float.NegativeInfinity;
    private float nextCombatCheck;
    private bool restartCombatMusic;
    private bool enemyChasing;

    public static AudioManager Instance => instance;

    /// <summary>The game-wide sounds and audio rules (null before the game has started its systems).</summary>
    public static AudioSettingsDefinition Sounds => instance != null ? instance.settings : null;

    /// <summary>Whether the music thinks the player is fighting.</summary>
    public AudioMood Mood { get; private set; } = AudioMood.Exploring;
    /// <summary>The current scene's music setup, or null when the scene has none (defaults play).</summary>
    public SceneAudio SceneMusic => sceneAudio;
    /// <summary>Which rule picked the current music, e.g. "Scene: combat" or "Default: exploring".</summary>
    public string MusicReason { get; private set; } = "";
    public MusicTrack CurrentMusic => music?.Track;
    public AudioSource CurrentMusicSource => music?.Source;
    public MusicTrack CurrentAmbience => ambience?.Track;

    /// <summary>Skips to the next track of the current music playlist, for testing playlists.</summary>
    public void SkipTrack() => music?.PlayNext();

    /// <summary>The player's volume for a channel, from 0 to 1, including the master volume.</summary>
    public static float ChannelVolume(AudioChannel channel)
    {
        return masterVolume * (volumes.TryGetValue(channel, out float volume) ? volume : 1f);
    }

    /// <summary>The player's own volume for a channel, without the master volume.</summary>
    public static float GetVolume(AudioChannel channel) => volumes.TryGetValue(channel, out float volume) ? volume : 1f;

    /// <summary>Changes and remembers the player's volume for a channel, for a settings menu.</summary>
    public static void SetVolume(AudioChannel channel, float volume)
    {
        volumes[channel] = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey + channel, volumes[channel]);
    }

    public static float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey + "Master", masterVolume);
        }
    }

    /// <summary>Where sounds are heard from: the player's head, or the camera while there is no player.</summary>
    public static Vector3 EarPosition
    {
        get
        {
            if (instance != null && instance.ears != null && instance.ears.enabled)
            {
                return instance.ears.transform.position;
            }

            Camera view = Camera.main;
            return view != null ? view.transform.position : Vector3.zero;
        }
    }

    /// <summary>
    /// How much of its volume a fade at this point (0..1) gives. Halfway through a plain fade both tracks are at half and the
    /// music sinks; this keeps the power of the two together steady.
    /// </summary>
    public static float EqualPower(float fade) => Mathf.Sin(Mathf.Clamp01(fade) * Mathf.PI * 0.5f);

    /// <summary>True when a new sound may take the voice of one already playing: only one of no more importance.</summary>
    public static bool CanCutOff(SoundPriority playing, SoundPriority incoming) => incoming >= playing && incoming != SoundPriority.Background;

    /// <summary>The volume a steady sound should sit at (a loop that follows the sliders), before any fade of its own.</summary>
    public static float SteadyVolume(SoundDefinition sound)
    {
        float world = sound.Positional && instance != null ? instance.settings.WorldSoundsLevel : 1f;
        return sound.Volume * ChannelVolume(sound.Channel) * world;
    }

    /// <summary>Plays a sound once at a position. Safe to call with no sound or before the manager exists.</summary>
    public static void Play(SoundDefinition sound, Vector3 position)
    {
        if (instance != null)
        {
            instance.PlayEffect(sound, position);
        }
    }

    /// <summary>
    /// Plays a sound for something of a given size, where 1 is the size it was made for: a bigger thing is lower, as it
    /// would be, and a smaller one higher and quieter. So one recording serves a sapling and an old oak.
    /// </summary>
    public static void Play(SoundDefinition sound, Vector3 position, float size)
    {
        if (instance != null)
        {
            instance.PlayEffect(sound, position, size);
        }
    }

    /// <summary>How much a sound's pitch moves for a thing of this size.</summary>
    public static float PitchForSize(float size) => Mathf.Clamp(Mathf.Pow(Mathf.Max(0.01f, size), -0.3f), 0.72f, 1.3f);

    /// <summary>How much of a sound's volume a thing of this size has. Nothing is louder than the sound itself.</summary>
    public static float VolumeForSize(float size) => Mathf.Clamp(0.5f + 0.5f * size, 0.6f, 1f);

    /// <summary>Sets up an audio source the way a sound asks: volume, pitch, 2D or 3D and distances.</summary>
    public static void Configure(AudioSource source, SoundDefinition sound)
    {
        float world = sound.Positional && instance != null ? instance.settings.WorldSoundsLevel : 1f;
        source.volume = sound.PickVolume() * ChannelVolume(sound.Channel) * world;
        source.pitch = sound.PickPitch();
        source.spatialBlend = sound.Positional ? 1f : 0f;
        // Sounds with a place are in the world, and stop when it does. The rest are the interface and warnings.
        source.ignoreListenerPause = !sound.Positional;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = sound.FullVolumeDistance;
        source.maxDistance = sound.AudibleDistance;
        source.dopplerLevel = 0f;
    }

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(AudioSettingsDefinition loaded)
    {
        instance = this;
        settings = loaded;
        masterVolume = PlayerPrefs.GetFloat(VolumeKey + "Master", settings.MasterVolume);
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
        {
            volumes[channel] = PlayerPrefs.GetFloat(VolumeKey + channel, settings.DefaultVolume(channel));
        }

        music = new LoopLayer(gameObject, AudioChannel.Music);
        ambience = new LoopLayer(gameObject, AudioChannel.Ambience);
        world = new WorldAudio(gameObject);
        var head = new GameObject("Ears");
        head.transform.SetParent(transform, false);
        ears = head.AddComponent<AudioListener>();
        ears.enabled = false;
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
        WatchPlayer(null);
        AudioListener.pause = false;
        if (instance == this)
        {
            instance = null;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        sceneAudio = FindAnyObjectByType<SceneAudio>();
        combatUntil = float.NegativeInfinity;
        lastDamageTime = float.NegativeInfinity;
        lastCombatEndedAt = float.NegativeInfinity;
        restartCombatMusic = false;
        enemyChasing = false;
        music.Forget();
        ambience.Forget();
        lastPlayedAt.Clear();
        lastClip.Clear();
        WatchPlayer(Player.Current != null ? Player.Current.Health : null);
        UseEars(Player.Current != null);
        world.OnSceneEntered();
        if (Player.Current != null && !Player.Current.TryGetComponent(out PlayerAudio _))
        {
            Player.Current.gameObject.AddComponent<PlayerAudio>();
        }
    }

    private void Update()
    {
        UpdateMood();
        // While a scene is being swapped its SceneAudio is already gone; keep the old music until the next scene says otherwise.
        bool sceneUnloading = !ReferenceEquals(sceneAudio, null) && sceneAudio == null;
        if (!sceneUnloading)
        {
            float raidTimeLeft = RaidDirector.Current != null ? RaidDirector.Current.Session.TimeRemaining : float.PositiveInfinity;
            MusicChoice choice = MusicRules.Choose(sceneAudio, settings, Mood, raidTimeLeft);
            MusicReason = choice.Reason;
            music.Switch(choice.Playlist);
            if (restartCombatMusic && choice.IsCombat)
            {
                music.Restart();
            }

            restartCombatMusic = false;
            ambience.Switch(sceneAudio != null ? sceneAudio.Ambience : null);
        }

        AudioListener.pause = GamePause.IsPaused;
        float fadeStep = FadeStep(settings.CrossfadeSeconds);
        music.Update(Mood == AudioMood.Combat ? FadeStep(settings.CombatFadeSeconds) : fadeStep);
        ambience.Update(fadeStep);
        world.Update(settings);
    }

    private static float FadeStep(float seconds) => seconds > 0f ? Time.unscaledDeltaTime / seconds : 1f;

    // After the camera has moved for the frame. Turned with the camera, so what is on the left of the screen is heard on the left.
    private void LateUpdate()
    {
        Player player = Player.Current;
        if ((player != null) != ears.enabled)
        {
            UseEars(player != null);
        }

        if (player == null)
        {
            return;
        }

        Camera view = Camera.main;
        Vector3 forward = view != null ? view.transform.forward : player.transform.forward;
        forward.y = 0f;
        Quaternion facing = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
        ears.transform.SetPositionAndRotation(player.transform.position + Vector3.up * EarHeight, facing);
    }

    // A scene brings its own listener on its camera. Only one may listen, and with no player it has to be the camera's.
    private void UseEars(bool onPlayer)
    {
        foreach (AudioListener listener in FindObjectsByType<AudioListener>())
        {
            if (listener != ears)
            {
                listener.enabled = !onPlayer;
            }
        }

        ears.enabled = onPlayer;
    }

    // ----- Combat -----

    private void UpdateMood()
    {
        if (Time.unscaledTime >= nextCombatCheck)
        {
            nextCombatCheck = Time.unscaledTime + CombatCheckInterval;
            enemyChasing = settings.EnemyChasingIsCombat && IsEnemyChasingPlayer();
        }

        bool fighting = enemyChasing
            || (settings.RecentDamageSeconds > 0f && Time.unscaledTime - lastDamageTime < settings.RecentDamageSeconds);
        if (fighting)
        {
            combatUntil = Time.unscaledTime + settings.CombatLingerSeconds;
        }

        AudioMood nextMood = player != null && player.IsAlive && Time.unscaledTime < combatUntil
            ? AudioMood.Combat
            : AudioMood.Exploring;
        if (Mood == AudioMood.Combat && nextMood != AudioMood.Combat)
        {
            lastCombatEndedAt = Time.unscaledTime;
        }
        else if (Mood != AudioMood.Combat && nextMood == AudioMood.Combat)
        {
            restartCombatMusic = MusicRules.ShouldRestartCombat(Time.unscaledTime, lastCombatEndedAt);
        }

        Mood = nextMood;
    }

    private bool IsEnemyChasingPlayer()
    {
        if (player == null)
        {
            return false;
        }

        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster.IsEngaging(player))
            {
                return true;
            }
        }

        return false;
    }

    private void WatchPlayer(Damageable next)
    {
        if (player != null)
        {
            player.Damaged -= OnPlayerDamaged;
            if (player.TryGetComponent(out Combatant combatant))
            {
                combatant.DamageDealt -= OnPlayerDealtDamage;
            }
        }

        player = next;
        if (player != null)
        {
            player.Damaged += OnPlayerDamaged;
            if (player.TryGetComponent(out Combatant combatant))
            {
                combatant.DamageDealt += OnPlayerDealtDamage;
            }
        }
    }

    private void OnPlayerDamaged(DamageInfo damage, DamageResult result)
    {
        if (damage.Source != null && damage.Source.Faction.WillAttack(Faction.Player))
        {
            lastDamageTime = Time.unscaledTime;
        }
    }

    // Breaking training dummies or walls isn't a fight; only hits on creatures that fight back count.
    private void OnPlayerDealtDamage(IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (target is Damageable hit && hit.Category == TargetCategory.Creature && hit.Faction.WillAttack(Faction.Player))
        {
            lastDamageTime = Time.unscaledTime;
        }
    }

    // ----- Death sounds -----

    // Pickups and deaths reach audio as game events like everything else, so sounds can be hung on other events the same way.
    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.ItemObtained && gameEvent.Subject is ItemDefinition item && gameEvent.ByPlayer)
        {
            Vector3 at = gameEvent.Actor != null ? gameEvent.Actor.transform.position : transform.position;
            Play(Rarity.StyleOf(item.Rarity).PickupSound, at);
            return;
        }

        SoundDefinition moment = MomentSound(gameEvent);
        if (moment != null)
        {
            Play(moment, gameEvent.Actor != null ? gameEvent.Actor.transform.position : transform.position);
            return;
        }

        bool died = gameEvent.Kind == GameEventKind.Killed || gameEvent.Kind == GameEventKind.Destroyed;
        if (died && gameEvent.Origin != null && gameEvent.Origin.TryGetComponent(out Damageable victim))
        {
            Play(DeathSoundFor(victim), victim.transform.position);
        }
    }

    // Quests, crafting, building and eating, from Audio Settings › World Sounds. Only the player's own moments.
    private SoundDefinition MomentSound(GameEvent gameEvent)
    {
        switch (gameEvent.Kind)
        {
            case GameEventKind.QuestStarted:
                return settings.QuestStarted;
            case GameEventKind.QuestCompleted:
                return settings.QuestCompleted;
            case GameEventKind.Crafted:
                return gameEvent.ByPlayer ? settings.Crafted : null;
            case GameEventKind.Built:
                return settings.Built;
            case GameEventKind.Consumed:
                return gameEvent.ByPlayer ? settings.Consumed : null;
            default:
                return null;
        }
    }

    /// <summary>The sound an object makes when it dies: its own, else its data's, else the default for its kind.</summary>
    public SoundDefinition DeathSoundFor(Damageable victim)
    {
        if (victim.TryGetComponent(out DeathSound own))
        {
            if (own.Silent)
            {
                return null;
            }

            if (own.Sound != null)
            {
                return own.Sound;
            }
        }

        if (victim.TryGetComponent(out IEventSubject known) && known.EventSubject is ICombatSounds sounds && sounds.DeathSound != null)
        {
            return sounds.DeathSound;
        }

        return settings.DefaultDeathSound(victim.Category);
    }

    // ----- Effects -----

    private void PlayEffect(SoundDefinition sound, Vector3 position, float size = 1f)
    {
        if (sound == null || !sound.HasClips || CountPlaying(sound) >= sound.MaxAtOnce || TooSoon(sound) || OutOfEarshot(sound, position))
        {
            return;
        }

        AudioSource source = TakeSource(sound.Priority);
        if (source == null)
        {
            return;
        }

        lastClip.TryGetValue(sound, out AudioClip last);
        AudioClip clip = sound.PickClip(last);
        lastClip[sound] = clip;
        lastPlayedAt[sound] = Time.unscaledTime;
        playing[source] = sound;
        source.transform.position = position;
        Configure(source, sound);
        source.pitch *= PitchForSize(size);
        source.volume *= VolumeForSize(size);
        source.clip = clip;
        source.Play();
    }

    private bool TooSoon(SoundDefinition sound) =>
        lastPlayedAt.TryGetValue(sound, out float last) && Time.unscaledTime - last < sound.MinimumGap;

    // Nobody can hear it, so it needn't take a voice from something they can.
    private static bool OutOfEarshot(SoundDefinition sound, Vector3 position) =>
        sound.Positional && (position - EarPosition).sqrMagnitude > sound.AudibleDistance * sound.AudibleDistance;

    private int CountPlaying(SoundDefinition sound)
    {
        int count = 0;
        foreach (KeyValuePair<AudioSource, SoundDefinition> entry in playing)
        {
            if (entry.Value == sound && entry.Key.isPlaying)
            {
                count++;
            }
        }

        return count;
    }

    // A free voice, a new one, or the voice of the least important sound playing, the oldest of those. Null when everything
    // playing matters more.
    private AudioSource TakeSource(SoundPriority priority)
    {
        AudioSource victim = null;
        SoundPriority lowest = SoundPriority.Important;
        foreach (AudioSource candidate in pool)
        {
            if (!candidate.isPlaying)
            {
                return candidate;
            }

            SoundPriority kind = playing.TryGetValue(candidate, out SoundDefinition sound) ? sound.Priority : SoundPriority.Normal;
            if (victim == null || kind < lowest || (kind == lowest && candidate.time > victim.time))
            {
                victim = candidate;
                lowest = kind;
            }
        }

        if (pool.Count < settings.MaxSimultaneousEffects)
        {
            var host = new GameObject("Effect " + pool.Count);
            host.transform.SetParent(transform, false);
            var created = host.AddComponent<AudioSource>();
            created.playOnAwake = false;
            pool.Add(created);
            return created;
        }

        if (victim == null || !CanCutOff(lowest, priority))
        {
            return null;
        }

        victim.Stop();
        return victim;
    }
}
