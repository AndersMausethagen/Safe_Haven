using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CinematicBuilder;

/// <summary>
/// The cinematic that ends the prologue (run with `unity command script run --file
/// AgentScripts/BuildPrologueEndCinematic.cs --entry BuildPrologueEndCinematic.Apply`): nine shots after
/// Docs/PrologueEndCinematic.md, written into Data/Cinematics/TheCatch.asset and set on the prologue's definition, whose
/// director plays it as the connection breaks, before home loads.
/// <para>
/// The paintings and their layers come from Tools/LocalSpawn/generate_prologue_end.py and the narration from
/// Tools/Audio/prologue_end_voice.py. Shot lengths follow the voice clips, so run this again after either changes.
/// Re-running rewrites the asset: change a shot here, not in the Inspector. The parts a shot is made of are
/// CinematicBuilder's, shared with the opening.
/// </para>
/// </summary>
public static class BuildPrologueEndCinematic
{
    private const string Art = "Assets/_Project/Art/Cinematics/PrologueEnd/";
    private const string Voice = "Assets/_Project/Audio/Voice/PrologueEnd/Catch_";
    private const string AssetPath = "Assets/_Project/Data/Cinematics/TheCatch.asset";
    private const string ProloguePath = "Assets/_Project/Data/Prologue/ForestPrologue.asset";
    private const string Nightsong = "Assets/_Project/Audio/Safe haven - Nightsong.mp3";
    private const string EerieRaid = "Assets/_Project/Audio/Misty woods - Eerie raid.mp3";
    private const string Wind = "Assets/_Project/Audio/Placeholder/Ambience_WildsWind.wav";
    private const string Rain = "Assets/_Project/Audio/SFX/OGA Rain Loops/Rain Loop 1.ogg";
    private const string Sfx = "Assets/_Project/Audio/SFX/";

    private static readonly Color PortalGold = new Color(1f, 0.86f, 0.5f, 1f);
    private static readonly Color Lightning = new Color(0.8f, 0.88f, 1f, 1f);
    private static readonly Color RainColour = new Color(0.75f, 0.83f, 0.95f, 0.5f);

    public static string Apply()
    {
        Use(Art, Voice);

        var shots = new List<CinematicShot>
        {
            HandOnTheStone(), OldOne(), RoadTears(), Scattered(), TheCatch(), Clearing(), Forgetting(), Hearth(), WakeUp(),
        };
        var music = new List<CinematicMusic>
        {
            // Cold and low under the storm and the fall.
            new CinematicMusic { clip = Clip(EerieRaid), fromShot = 0, delay = 0.5f, toShot = 4, startAt = 0f, volume = 0.34f, fadeIn = 3f, fadeOut = 3f },
            // Warm from the dawn clearing to the end. The forgetting stays under it, quieter by its own sounds.
            new CinematicMusic { clip = Clip(Nightsong), fromShot = 5, delay = 0f, toShot = 8, startAt = 0f, volume = 0.58f, fadeIn = 3f, fadeOut = 2.5f },
        };

        CinematicDefinition definition = Save(AssetPath, "The Catch", shots, music);
        string wired = WirePrologue(definition);
        var timeline = new CinematicTimeline(definition);
        return $"The Catch: {shots.Count} shots, {timeline.Lines.Count} lines, {timeline.Seconds:0.0} s. {wired}";
    }

    // ----- The shots -----

    // 1. The hand on the stone: the runes wake under the palm.
    private static CinematicShot HandOnTheStone()
    {
        CinematicShot shot = Shot("The Hand On The Stone", CinematicTransition.ThroughBlack, 2f, new CinematicPose(0f, 0f, 0f), new CinematicPose(0f, 0.008f, 0.06f), 1f,
            ("c01a", "You put your hand on the stone, little one.", 1.5f),
            ("c01b", "And far away, in my sleep, I felt it.", 0.9f));
        float size = Painting(shot, "c01_hand");
        // The light spreads from the palm as the second line is said.
        float felt = Share(shot, LineStart(shot, 1));
        CinematicLayer wake = Glow("Runes Wake", HandU, HandV, 0.7f, 0f, size, PortalGold, 0.5f);
        wake.opacity = Keys((0f, 0.25f), (felt, 0.45f), (felt + 0.2f, 1f), (1f, 0.85f));
        shot.layers.Insert(shot.layers.Count - 1, wake);
        shot.layers.Add(RainFall(90));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Rain, 0f, 0.3f, 1.5f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Moments/Ward Toll.wav", LineStart(shot, 1) + 0.2f, 0.22f, 0.3f, loop: false));
        return shot;
    }

    // 2. The old one above the trees, and the ring small below it.
    private static CinematicShot OldOne()
    {
        CinematicShot shot = Shot("The Old One", CinematicTransition.Dissolve, 1.6f, new CinematicPose(0f, -0.03f, 0.03f), new CinematicPose(0f, 0.03f, 0.03f), 0.9f,
            ("c02a", "The old one felt it too.", 1f),
            ("c02b", "It was already walking towards the stones.", 0.7f));
        float size = Painting(shot, "c02_old_one");
        // Lightning behind it, twice: once far, once near.
        CinematicLayer flash = Glow("Lightning", BoltU, BoltV, 1.1f, Depths[4][0], size, Lightning, 0.55f);
        flash.opacity = Keys((0f, 0f), (0.18f, 0f), (0.2f, 0.8f), (0.24f, 0.1f), (0.26f, 0.6f), (0.34f, 0f), (0.7f, 0f), (0.72f, 1f), (0.75f, 0.2f), (0.78f, 0.7f), (0.88f, 0f), (1f, 0f));
        shot.layers.Insert(1, flash);
        // The two torches in the ring gutter in the rain.
        foreach ((float u, float v) in new[] { (0.398f, 0.275f), (0.504f, 0.268f) })
        {
            shot.layers.Insert(shot.layers.Count - 1, Glow("Torch", u, v, 0.16f, 0f, size, Firelight, 0.45f, flicker: 0.45f));
        }

        shot.layers.Add(RainFall(110));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Rain, 0f, 0.3f, 1f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven World/Troll Step 2.wav", 0.6f, 0.4f, 0.1f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven World/Troll Step 4.wav", LineStart(shot, 1) + 0.4f, 0.5f, 0.1f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven World/Troll Step 1.wav", LineStart(shot, 1) + 2.4f, 0.6f, 0.1f, loop: false));
        return shot;
    }

    // 3. The blow lands as the way opens.
    private static CinematicShot RoadTears()
    {
        CinematicShot shot = Shot("The Road Tears", CinematicTransition.Cut, 0f, new CinematicPose(0f, 0f, 0f), new CinematicPose(0f, 0f, 0.1f), 1.3f,
            ("c03a", "Its blow fell as the way opened.", 0.6f),
            ("c03b", "The stones broke, and the road tore.", 0.7f));
        shot.easeCamera = true;
        float size = Painting(shot, "c03_break");
        // The burst: bright on the blow, flaring on "broke", gone as the line ends.
        float broke = Share(shot, LineStart(shot, 1) + shot.lines[1].Seconds * 0.35f);
        float tore = Share(shot, LineStart(shot, 1) + shot.lines[1].Seconds);
        CinematicLayer burst = Glow("Burst", BurstU, BurstV, 1.5f, 0f, size, new Color(1f, 0.93f, 0.7f, 1f), 0.75f, flicker: 0.25f);
        burst.opacity = Keys((0f, 0.55f), (broke - 0.05f, 0.6f), (broke, 1f), (tore, 0.35f), (tore + 0.08f, 0f), (1f, 0f));
        shot.layers.Insert(shot.layers.Count - 1, burst);
        shot.layers.Add(Specks("Shards", 0f, new Color(1f, 0.85f, 0.5f, 0.9f), additive: true, new CinematicMotes
        {
            count = 60, size = new Vector2(0.003f, 0.01f), velocity = new Vector2(0.09f, 0.05f), wander = 0.12f, wanderSeconds = 1.2f,
            twinkle = 0.5f, area = new Rect(0.1f, 0.1f, 0.8f, 0.8f), edgeFade = 0.25f,
        }));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Rain, 0f, 0.26f, 0.3f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven World/Stone Breaks 2.wav", LineStart(shot, 0) + 0.3f, 0.6f, 0.05f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Moments/Ward Falls.wav", LineStart(shot, 1) + shot.lines[1].Seconds * 0.3f, 0.55f, 0.05f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven World/Stone Breaks 3.wav", LineStart(shot, 1) + shot.lines[1].Seconds * 0.4f, 0.5f, 0.05f, loop: false));
        return shot;
    }

    // 4. Seven lights thrown far apart over the dark forest.
    private static CinematicShot Scattered()
    {
        CinematicShot shot = Shot("Scattered", CinematicTransition.Dissolve, 1.6f, new CinematicPose(0f, 0f, 0.22f), new CinematicPose(0f, 0f, 0f), 1.3f,
            ("c04a", "Everyone in that ring was thrown into the dark.", 1f),
            ("c04b", "Seven of your people are out there now, scattered like sparks across the Misty Woods.", 0.8f));
        shot.easeCamera = true;
        float size = Painting(shot, "c04_scattered");
        // Each spark shows as it lands, burns, and sinks to a point that stays. The painting looks out over the forest
        // from a hill, so the far ones are small and near the horizon (the sky begins at about 0.72 up).
        var sparks = new[] { (0.3f, 0.42f), (0.7f, 0.44f), (0.08f, 0.56f), (0.86f, 0.6f), (0.44f, 0.6f), (0.26f, 0.66f), (0.66f, 0.68f) };
        float counted = Share(shot, LineStart(shot, 1));
        for (int i = 0; i < sparks.Length; i++)
        {
            bool far = sparks[i].Item2 >= 0.58f;
            float lands = 0.06f + i * (counted - 0.06f) / sparks.Length;
            CinematicLayer spark = Glow($"Spark {i + 1}", sparks[i].Item1, sparks[i].Item2, far ? 0.06f : 0.1f, far ? Depths[2][0] : 0f, size, Firelight, 0.9f, flicker: 0.3f);
            spark.opacity = Keys((0f, 0f), (lands, 0f), (lands + 0.03f, 1f), (lands + 0.2f, 0.75f), (1f, 0.3f));
            shot.layers.Add(spark);
        }

        shot.layers.Add(Mist("Mist Over The Trees", -0.3f, new Vector2(0.01f, 0f), 0.3f));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.3f, 1.2f));
        return shot;
    }

    // 5. Falling, and the light that takes one of them.
    private static CinematicShot TheCatch()
    {
        CinematicShot shot = Shot("The Catch", CinematicTransition.Dissolve, 1.6f, new CinematicPose(0f, 0.04f, 0f), new CinematicPose(0f, -0.02f, 0.06f), 1.4f,
            ("c05a", "I am old, and thin, and I had slept a hundred winters.", 0.9f),
            ("c05b", "But I reached.", 0.9f),
            ("c05c", "I caught one. I could only catch one.", 0.9f));
        shot.easeCamera = true;
        float size = Painting(shot, "c05_catch");
        float reached = Share(shot, LineStart(shot, 1));
        float caught = Share(shot, LineStart(shot, 2));
        CinematicLayer light = Glow("The Door's Light", CatchU, CatchV, 1.1f, 0.5f, size, PortalGold, 0.6f);
        light.opacity = Keys((0f, 0.15f), (reached, 0.25f), (reached + 0.08f, 1f), (caught, 0.9f), (1f, 0.7f));
        shot.layers.Insert(1, light);
        shot.layers.Add(Specks("Motes", 0f, PortalGold, additive: true, new CinematicMotes
        {
            count = 50, size = new Vector2(0.002f, 0.007f), velocity = new Vector2(0.004f, 0.06f), wander = 0.03f, wanderSeconds = 3f,
            twinkle = 0.7f, area = new Rect(0.15f, 0f, 0.7f, 0.9f), edgeFade = 0.3f,
        }));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.22f, 1f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Moments/Ward Mended.wav", LineStart(shot, 1) + 0.1f, 0.4f, 0.2f, loop: false));
        return shot;
    }

    // 6. Dawn: the clearing, and the one who was caught asleep at the foot of the stones.
    private static CinematicShot Clearing()
    {
        CinematicShot shot = Shot("The Clearing", CinematicTransition.Dissolve, 2.2f, new CinematicPose(0f, -0.01f, 0.09f), new CinematicPose(0f, 0.005f, 0f), 1.2f,
            ("c06a", "So here you are: my clearing.", 1.3f),
            ("c06b", "The last place the forest has not taken.", 0.7f));
        shot.easeCamera = true;
        Painting(shot, "c06_clearing");
        shot.layers.Add(Specks("Fireflies", 0.5f, new Color(0.9f, 1f, 0.6f, 0.9f), additive: true, new CinematicMotes
        {
            count = 24, size = new Vector2(0.002f, 0.005f), velocity = new Vector2(0.006f, 0.004f), wander = 0.04f, wanderSeconds = 4f,
            twinkle = 0.9f, area = new Rect(0.1f, 0.15f, 0.8f, 0.6f), edgeFade = 0.25f,
        }));
        shot.layers.Add(Mist("Morning Mist", 1.5f, new Vector2(0.008f, 0f), 0.2f));
        shot.layers.Add(Vignette(0.5f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 1.wav", 1.2f, 0.2f, 0.2f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 3.wav", 4.6f, 0.16f, 0.2f, loop: false));
        return shot;
    }

    // 7. One of the lost, far off; the trunks slide across and the mist closes.
    private static CinematicShot Forgetting()
    {
        CinematicShot shot = Shot("The Forgetting", CinematicTransition.Dissolve, 1.8f, new CinematicPose(-0.03f, 0f, 0.02f), new CinematicPose(0.03f, 0f, 0.02f), 1.1f,
            ("c07a", "The others are alive.", 1f),
            ("c07b", "But the forest does not kill the lost. It forgets them.", 0.7f),
            ("c07c", "And it has already begun.", 0.8f));
        float size = Painting(shot, "c07_lost");
        float begun = Share(shot, LineStart(shot, 2) + shot.lines[2].Seconds * 0.6f);
        CinematicLayer lantern = Glow("Lantern", LostU, LostV, 0.12f, Depths[4][1], size, Firelight, 0.7f, flicker: 0.3f);
        lantern.opacity = Keys((0f, 1f), (begun - 0.12f, 0.9f), (begun, 0.25f), (1f, 0.12f));
        shot.layers.Insert(1, lantern);
        CinematicLayer mist = Mist("Mist Closing In", 0.6f, new Vector2(0.012f, 0.003f), 0.6f);
        mist.opacity = Keys((0f, 0.2f), (1f, 1f));
        shot.layers.Insert(3, mist);
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.26f, 1.2f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Woodcutting/Tree Creak 4.wav", 2.2f, 0.26f, 0.2f, loop: false));
        return shot;
    }

    // 8. A fire beside the stones, and something far off that answers it.
    private static CinematicShot Hearth()
    {
        CinematicShot shot = Shot("A Hearth", CinematicTransition.Dissolve, 1.8f, new CinematicPose(0f, 0f, 0f), new CinematicPose(0f, -0.006f, 0.07f), 1.2f,
            ("c08a", "There is one thing it cannot forget: a hearth that someone tends.", 1f),
            ("c08b", "Light one beside my stones, and they will see it, and know which way is home.", 0.8f));
        float size = Painting(shot, "c08_hearth");
        shot.layers.Insert(shot.layers.Count - 1, Glow("Firelight", FireU, FireV, 0.8f, 0f, size, Firelight, 0.4f, flicker: 0.3f));
        // "They will see it": one small light answers from the dark of the trees.
        float seen = Share(shot, LineStart(shot, 1) + shot.lines[1].Seconds * 0.5f);
        CinematicLayer answer = Glow("A Light Answers", AnswerU, AnswerV, 0.07f, Depths[4][0], size, Firelight, 0.9f, flicker: 0.2f);
        answer.opacity = Keys((0f, 0f), (seen, 0f), (seen + 0.05f, 1f), (seen + 0.2f, 0.5f), (1f, 0.7f));
        shot.layers.Insert(1, answer);
        shot.layers.Add(Specks("Sparks", 0f, Ember, additive: true, new CinematicMotes
        {
            count = 20, size = new Vector2(0.002f, 0.006f), velocity = new Vector2(0.008f, 0.1f), wander = 0.02f, wanderSeconds = 2f,
            twinkle = 0.5f, area = new Rect(FireU - 0.07f, FireV - 0.03f, 0.14f, 0.45f), edgeFade = 0.3f,
        }));
        shot.layers.Add(Vignette(0.75f));
        return shot;
    }

    // 9. The clearing again, closer: the runes brighten, and the picture goes to white as the sleeper is told to wake.
    private static CinematicShot WakeUp()
    {
        CinematicShot shot = Shot("Wake Up", CinematicTransition.Dissolve, 2f, new CinematicPose(SleeperX, SleeperY, 0.08f), new CinematicPose(SleeperX, SleeperY, 0.2f), 2f,
            ("c09a", "Then build them something to come home to.", 1f),
            ("c09b", "Wake me, and I will open the way, and you will go and bring them back.", 0.8f),
            ("c09c", "Now wake up, little one.", 1.2f));
        shot.easeCamera = true;
        float size = Painting(shot, "c06_clearing");
        float wake = Share(shot, LineStart(shot, 2));
        CinematicLayer runes = Glow("Runes", StonesU, StonesV, 0.6f, 0f, size, PortalGold, 0.45f);
        runes.opacity = Keys((0f, 0.1f), (wake, 0.6f), (1f, 1f));
        shot.layers.Insert(shot.layers.Count - 1, runes);
        // Morning grows until it is all there is.
        CinematicLayer morning = Glow("Morning", 0.5f, 0.6f, 3.2f, Pinned, 1f, new Color(1f, 0.97f, 0.88f, 1f), 1f);
        morning.opacity = Keys((0f, 0f), (wake, 0.05f), (Share(shot, LineStart(shot, 2) + shot.lines[2].Seconds), 0.55f), (1f, 1f));
        shot.layers.Add(morning);
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 2.wav", 0.8f, 0.2f, 0.2f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 4.wav", 5.2f, 0.18f, 0.2f, loop: false));
        return shot;
    }

    // ----- Where things are in the paintings (u, v from the bottom left), read off the pictures -----

    private const float HandU = 0.63f, HandV = 0.49f;
    private const float BoltU = 0.37f, BoltV = 0.76f;
    private const float BurstU = 0.49f, BurstV = 0.36f;
    private const float CatchU = 0.655f, CatchV = 0.6f;
    private const float LostU = 0.44f, LostV = 0.19f;
    private const float FireU = 0.513f, FireV = 0.275f;
    private const float AnswerU = 0.72f, AnswerV = 0.52f;
    private const float StonesU = 0.44f, StonesV = 0.59f;
    // The camera's aim in the last shot: the sleeper, as a share of the frame from its centre.
    private const float SleeperX = -0.02f, SleeperY = -0.12f;

    private static CinematicLayer RainFall(int count) => Specks("Rain", -0.2f, RainColour, additive: false, new CinematicMotes
    {
        count = count, size = new Vector2(0.0015f, 0.004f), velocity = new Vector2(-0.12f, -0.9f), wander = 0.01f, wanderSeconds = 1f,
        twinkle = 0.1f, edgeFade = 0.05f,
    });

    // ----- The prologue -----

    // Sets the cinematic on the prologue's definition, which the director plays before home loads. Only that asset is saved.
    private static string WirePrologue(CinematicDefinition definition)
    {
        var prologue = AssetDatabase.LoadAssetAtPath<PrologueDefinition>(ProloguePath);
        if (prologue == null)
        {
            return "No prologue definition at " + ProloguePath + ": not wired.";
        }

        var serialized = new SerializedObject(prologue);
        SerializedProperty field = serialized.FindProperty("endingCinematic");
        if (field.objectReferenceValue == definition)
        {
            return "The prologue already ends with it.";
        }

        field.objectReferenceValue = definition;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prologue);
        AssetDatabase.SaveAssetIfDirty(prologue);
        return "Set as the prologue's ending cinematic.";
    }
}
