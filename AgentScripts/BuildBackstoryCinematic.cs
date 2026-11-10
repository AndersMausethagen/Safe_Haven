using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CinematicBuilder;

/// <summary>
/// The backstory cinematic that opens a new game (run with `unity command script run --file
/// AgentScripts/BuildBackstoryCinematic.cs --entry BuildBackstoryCinematic.Apply`): twelve shots after
/// Docs/BackstoryCinematic.md, written into Data/Cinematics/Backstory.asset and set on the main menu.
/// <para>
/// The paintings and their layers come from Tools/LocalSpawn/generate_backstory.py, the overlays from
/// Tools/Art/cinematic_overlays.py and the narration from Tools/Audio/narration.py. Shot lengths follow the voice clips, so
/// run this again after any of them changes. Re-running rewrites the asset: change a shot here, not in the Inspector.
/// The parts a shot is made of (Shot, Painting, Glow, Mist...) are CinematicBuilder's, shared with the other cinematics.
/// </para>
/// </summary>
public static class BuildBackstoryCinematic
{
    private const string Art = "Assets/_Project/Art/Cinematics/Backstory/";
    private const string Voice = "Assets/_Project/Audio/Voice/Backstory/Backstory_";
    private const string AssetPath = "Assets/_Project/Data/Cinematics/Backstory.asset";
    private const string MenuScene = "Assets/_Project/Scenes/MainMenu.unity";
    private const string Nightsong = "Assets/_Project/Audio/Safe haven - Nightsong.mp3";
    private const string EerieRaid = "Assets/_Project/Audio/Misty woods - Eerie raid.mp3";
    private const string Wind = "Assets/_Project/Audio/Placeholder/Ambience_WildsWind.wav";
    private const string Sfx = "Assets/_Project/Audio/SFX/";

    public static string Apply()
    {
        Use(Art, Voice);

        var shots = new List<CinematicShot>
        {
            Embers(), Promise(), Hearth(), Seasons(), FireGoesOut(), WayHome(), TheirNames(), NoOneLeft(),
            OneClearing(), FourWentIn(), FourMore(), MorningFire(),
        };
        var music = new List<CinematicMusic>
        {
            // Warm under the pact; gone by the first word of the cold hearth.
            new CinematicMusic { clip = Clip(Nightsong), fromShot = 0, delay = 2.5f, toShot = 3, startAt = 0f, volume = 0.6f, fadeIn = 3f, fadeOut = 3f },
            // Cold under the forgetting. It is the louder track, so it sits lower.
            new CinematicMusic { clip = Clip(EerieRaid), fromShot = 5, delay = 0f, toShot = 9, startAt = 0f, volume = 0.36f, fadeIn = 4f, fadeOut = 2.5f },
            // And the warm one again from the cut to daylight.
            new CinematicMusic { clip = Clip(Nightsong), fromShot = 10, delay = 0f, toShot = 11, startAt = 62f, volume = 0.6f, fadeIn = 1.2f, fadeOut = 2.5f },
        };

        CinematicDefinition definition = Save(AssetPath, "The Old Story", shots, music);
        string menu = WireMainMenu(definition);
        var timeline = new CinematicTimeline(definition);
        return $"Backstory: {shots.Count} shots, {timeline.Lines.Count} lines, {timeline.Seconds:0.0} s. {menu}";
    }

    // ----- The shots -----

    // 1. Black, a fire just out of frame.
    private static CinematicShot Embers()
    {
        CinematicShot shot = Shot("Embers", CinematicTransition.ThroughBlack, 2.4f, new CinematicPose(0f, 0f, 0f), new CinematicPose(0f, 0.02f, 0f), 1f,
            ("s01a", "Listen, little one.", 2.2f),
            ("s01b", "This is an old story, and I am nearly the last who remembers it.", 0.9f));
        shot.layers.Add(Glow("Fire Below", 0.5f, -0.12f, 1.5f, 0f, 1f, Firelight, 0.5f, flicker: 0.35f));
        shot.layers.Add(Specks("Embers", 0.5f, Ember, additive: true, new CinematicMotes
        {
            count = 46, size = new Vector2(0.004f, 0.011f), velocity = new Vector2(0.006f, 0.085f), wander = 0.035f, wanderSeconds = 3.2f,
            twinkle = 0.5f, area = new Rect(0.1f, 0f, 0.8f, 0.95f), edgeFade = 0.3f,
        }));
        shot.layers.Add(Specks("Far Embers", 3f, Ember, additive: true, new CinematicMotes
        {
            count = 30, size = new Vector2(0.002f, 0.005f), velocity = new Vector2(-0.004f, 0.05f), wander = 0.02f, wanderSeconds = 4f,
            twinkle = 0.6f, area = new Rect(0f, 0f, 1f, 0.8f), edgeFade = 0.35f,
        }));
        shot.sounds.Add(Bed(Wind, 0f, 0.1f, 1.5f));
        return shot;
    }

    // 2. The promise: people, guardians and the stones between them.
    private static CinematicShot Promise()
    {
        var from = new CinematicPose(0f, 0f, 0f);
        var to = new CinematicPose(0f, 0.012f, 0.07f);
        CinematicShot shot = Shot("The Promise", CinematicTransition.Dissolve, 2.2f, from, to, 1.2f,
            ("s02a", "Long ago, your people made a promise with the forest.", 1.3f),
            ("s02b", "They would leave it room to live.", 0.75f),
            ("s02c", "And its guardians would keep older, hungrier things at bay.", 0.75f));
        float size = Painting(shot, "s02_promise");
        // The runes breathe.
        CinematicLayer runes = Glow("Rune Light", 0.42f, 0.56f, 0.5f, 1f, size, RuneGold, 0.22f);
        runes.opacity = Keys((0f, 0.35f), (0.25f, 1f), (0.5f, 0.4f), (0.75f, 1f), (1f, 0.5f));
        shot.layers.Insert(2, runes);
        shot.layers.Add(Specks("Pollen", 0.3f, new Color(1f, 0.96f, 0.75f, 0.8f), additive: true, new CinematicMotes
        {
            count = 55, size = new Vector2(0.002f, 0.006f), velocity = new Vector2(0.012f, 0.008f), wander = 0.03f, wanderSeconds = 5f,
            twinkle = 0.7f, area = new Rect(0f, 0.1f, 1f, 0.85f), edgeFade = 0.2f,
        }));
        shot.layers.Add(Vignette(0.55f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 1.wav", 0.8f, 0.22f, 0.3f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 3.wav", 4.4f, 0.18f, 0.3f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 2.wav", 8.6f, 0.2f, 0.3f, loop: false));
        return shot;
    }

    // 3. The hearth beside the stone.
    private static CinematicShot Hearth()
    {
        var from = new CinematicPose(-0.02f, 0f, 0f);
        var to = new CinematicPose(0.02f, 0f, 0.035f);
        CinematicShot shot = Shot("The Hearth", CinematicTransition.Dissolve, 1.8f, from, to, 1.5f,
            ("s03a", "Beside my stones they built a hearth.", 1.1f),
            ("s03b", "They kept the fire, and I kept the night from their door.", 0.8f),
            ("s03c", "That was the pact.", 1f));
        float size = Painting(shot, "s03_hearth");
        shot.layers.Insert(3, Glow("Firelight", 0.517f, 0.3f, 0.75f, 0f, size, Firelight, 0.4f, flicker: 0.3f));
        // The stone answers "I kept the night from their door".
        float answer = Share(shot, LineStart(shot, 1) + shot.lines[1].Seconds * 0.45f);
        CinematicLayer rune = Glow("Rune Answers", 0.585f, 0.56f, 0.55f, 0f, size, RuneGold, 0.6f);
        rune.opacity = Keys((0f, 0f), (answer - 0.04f, 0f), (answer + 0.05f, 1f), (answer + 0.22f, 0.15f), (1f, 0.1f));
        shot.layers.Insert(4, rune);
        shot.layers.Add(Specks("Sparks", 0f, Ember, additive: true, new CinematicMotes
        {
            count = 22, size = new Vector2(0.003f, 0.007f), velocity = new Vector2(0.01f, 0.11f), wander = 0.02f, wanderSeconds = 2f,
            twinkle = 0.5f, area = new Rect(0.44f, 0.24f, 0.16f, 0.5f), edgeFade = 0.3f,
        }));
        shot.layers.Add(Vignette(0.7f));
        return shot;
    }

    // 4. The same village through three seasons; the fire burns in all of them.
    private static CinematicShot Seasons()
    {
        var from = new CinematicPose(0f, 0f, 0.09f);
        var to = new CinematicPose(0f, 0f, 0f);
        CinematicShot shot = Shot("The Seasons", CinematicTransition.Dissolve, 1.8f, from, to, 3.4f,
            ("s04a", "It held longer than anyone has counted.", 2.6f));
        // The pull back settles; it must not run on past the whole picture.
        shot.easeCamera = true;
        // Whole paintings, one fading in over the last: summer, autumn, winter. Nothing here has parallax, only the pull back.
        CinematicLayer summer = Picture("Summer", Art + "s04_summer_0_whole.png", 0f, 1f);
        CinematicLayer autumn = Picture("Autumn", Art + "s04_autumn_0_whole.png", 0f, 1f);
        autumn.opacity = Keys((0f, 0f), (0.24f, 0f), (0.42f, 1f), (1f, 1f));
        CinematicLayer winter = Picture("Winter", Art + "s04_winter_0_whole.png", 0f, 1f);
        winter.opacity = Keys((0f, 0f), (0.58f, 0f), (0.78f, 1f), (1f, 1f));
        shot.layers.AddRange(new[] { summer, autumn, winter });
        CinematicLayer leaves = Specks("Leaves", 0f, new Color(0.95f, 0.6f, 0.2f, 0.9f), additive: false, new CinematicMotes
        {
            count = 26, size = new Vector2(0.006f, 0.012f), velocity = new Vector2(0.03f, -0.07f), wander = 0.04f, wanderSeconds = 2.5f,
            twinkle = 0.3f, edgeFade = 0.15f,
        });
        leaves.opacity = Keys((0f, 0f), (0.3f, 0f), (0.42f, 1f), (0.6f, 1f), (0.72f, 0f), (1f, 0f));
        CinematicLayer snow = Snow("Snow", 60);
        snow.opacity = Keys((0f, 0f), (0.62f, 0f), (0.8f, 1f), (1f, 1f));
        shot.layers.Add(leaves);
        shot.layers.Add(snow);
        shot.layers.Add(Vignette(0.5f));
        return shot;
    }

    // 5. The hearth again, cold. The one shot with no music under it.
    private static CinematicShot FireGoesOut()
    {
        var from = new CinematicPose(0.02f, 0f, 0.035f);
        var to = new CinematicPose(0.02f, 0f, 0.08f);
        CinematicShot shot = Shot("The Fire Goes Out", CinematicTransition.Dissolve, 2.4f, from, to, 3f,
            ("s05a", "Then, one winter, the fire went out.", 2.2f));
        shot.easeCamera = true;
        float size = Painting(shot, "s05_cold_hearth");
        // The last ember: alive through the line, dead as it ends.
        float lineEnd = Share(shot, LineStart(shot, 0) + shot.lines[0].Seconds);
        CinematicLayer ember = Glow("Last Ember", 0.583f, 0.165f, 0.16f, 0f, size, Ember, 0.95f, flicker: 0.45f);
        ember.opacity = Keys((0f, 0.9f), (lineEnd - 0.2f, 0.75f), (lineEnd, 0.3f), (lineEnd + 0.07f, 0f), (1f, 0f));
        shot.layers.Insert(3, ember);
        shot.layers.Add(Snow("Snow", 70));
        shot.layers.Add(Vignette(0.9f));
        shot.sounds.Add(Bed(Wind, 0f, 0.3f, 1.5f));
        return shot;
    }

    // 6a. First the way home.
    private static CinematicShot WayHome()
    {
        var from = new CinematicPose(0f, 0f, 0f);
        var to = new CinematicPose(0f, 0.005f, 0.08f);
        CinematicShot shot = Shot("The Way Home", CinematicTransition.Cut, 0f, from, to, 0.9f,
            ("s06a", "The forest does not kill the lost.", 1.1f),
            ("s06b", "It forgets them.", 0.9f),
            ("s06c", "First the way home.", 1.1f));
        Painting(shot, "s06a_trail");
        // The mist comes forward and closes the trail.
        CinematicLayer mist = Mist("Mist Closing In", 0.6f, new Vector2(0.012f, 0.004f), 0.6f);
        mist.opacity = Keys((0f, 0.15f), (1f, 1f));
        shot.layers.Insert(2, mist);
        shot.layers.Add(Vignette(0.9f));
        shot.sounds.Add(Bed(Wind, 0f, 0.26f, 1.2f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Woodcutting/Tree Creak 2.wav", 2.4f, 0.3f, 0.2f, loop: false));
        return shot;
    }

    // 6b. Then their names.
    private static CinematicShot TheirNames()
    {
        var from = new CinematicPose(0f, 0.035f, 0.05f);
        var to = new CinematicPose(0f, -0.035f, 0.05f);
        CinematicShot shot = Shot("Their Names", CinematicTransition.Cut, 0f, from, to, 1.4f,
            ("s06d", "Then their names.", 0.7f));
        Painting(shot, "s06b_names");
        shot.layers.Add(Mist("Mist", -0.2f, new Vector2(-0.01f, 0f), 0.4f));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.28f, 0.6f));
        return shot;
    }

    // 6c. Then there is no one left to find.
    private static CinematicShot NoOneLeft()
    {
        var from = new CinematicPose(0.025f, 0f, 0.02f);
        var to = new CinematicPose(-0.025f, 0f, 0.02f);
        CinematicShot shot = Shot("No One Left", CinematicTransition.Cut, 0f, from, to, 2f,
            ("s06e", "Then there is no one left to find.", 0.9f));
        Painting(shot, "s06c_empty_village");
        shot.layers.Insert(2, Mist("Mist Between Houses", 0.2f, new Vector2(0.014f, 0f), 0.45f));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.3f, 0.8f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Woodcutting/Tree Creak 4.wav", 1.6f, 0.28f, 0.2f, loop: false));
        return shot;
    }

    // 7. One clearing in the dark, seen from above: the largest move, pulling back until it is small.
    private static CinematicShot OneClearing()
    {
        var from = new CinematicPose(0.04f, -0.05f, 0.3f);
        var to = new CinematicPose(0f, 0f, 0f);
        CinematicShot shot = Shot("One Clearing", CinematicTransition.Dissolve, 1.8f, from, to, 2.2f,
            ("s07a", "A hundred winters I kept one small clearing, alone.", 1.3f),
            ("s07b", "I grew thin.", 1.1f),
            ("s07c", "And at last, I slept.", 1.2f));
        shot.easeCamera = true;
        float size = Painting(shot, "s07_clearing");
        // The ward's light beats slower and slower, and is out by "I slept".
        float slept = Share(shot, LineStart(shot, 2) + shot.lines[2].Seconds);
        CinematicLayer ward = Glow("Ward", 0.62f, 0.37f, 0.85f, 2f, size, new Color(0.75f, 1f, 0.5f, 1f), 0.4f);
        ward.opacity = Keys((0f, 1f), (0.08f, 0.5f), (0.17f, 1f), (0.3f, 0.45f), (0.45f, 0.9f), (0.63f, 0.35f), (slept - 0.06f, 0.6f), (slept, 0f), (1f, 0f));
        shot.layers.Insert(1, ward);
        CinematicLayer fireflies = Specks("Fireflies", 1f, new Color(0.85f, 1f, 0.55f, 0.9f), additive: true, new CinematicMotes
        {
            count = 26, size = new Vector2(0.003f, 0.006f), velocity = new Vector2(0.006f, 0.004f), wander = 0.04f, wanderSeconds = 4f,
            twinkle = 0.9f, area = new Rect(0.42f, 0.2f, 0.4f, 0.4f), edgeFade = 0.25f,
        });
        fireflies.opacity = Keys((0f, 1f), (slept - 0.25f, 0.6f), (slept, 0f), (1f, 0f));
        shot.layers.Add(fireflies);
        shot.layers.Add(Mist("Mist Over The Trees", -0.3f, new Vector2(0.01f, 0f), 0.25f));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.24f, 1.5f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Living World/Owl Hoot 2.wav", 2f, 0.16f, 0.2f, loop: false));
        return shot;
    }

    // 8. Four walk in with lanterns. The lanterns go out one by one, the last on "They did not walk out".
    private static CinematicShot FourWentIn()
    {
        var from = new CinematicPose(0f, 0f, 0f);
        var to = new CinematicPose(0f, -0.006f, 0.075f);
        CinematicShot shot = Shot("Four Went In", CinematicTransition.Dissolve, 1.8f, from, to, 2f,
            ("s08a", "Then four of your people walked in under the trees, to learn what the woods had become.", 1.3f),
            ("s08b", "They did not walk out.", 1.3f));
        float size = Painting(shot, "s08_four_went_in");
        // The travellers are in the painting's far layer, so their lanterns' light and dark stand at its distance.
        float far = Depths[4][0];
        float firstLineEnd = Share(shot, LineStart(shot, 0) + shot.lines[0].Seconds);
        float lastLine = Share(shot, LineStart(shot, 1) + shot.lines[1].Seconds * 0.7f);
        var lanterns = new[] { (0.548f, 0.208f, firstLineEnd - 0.14f), (0.479f, 0.191f, firstLineEnd + 0.02f), (0.411f, 0.196f, lastLine) };
        int at = 1;
        foreach ((float u, float v, float out_) in lanterns)
        {
            CinematicLayer light = Glow("Lantern Light", u, v, 0.2f, far, size, Firelight, 0.5f, flicker: 0.25f);
            light.opacity = Keys((0f, 1f), (out_ - 0.02f, 1f), (out_ + 0.015f, 0f), (1f, 0f));
            // A dark smudge over the painted flame, so the lantern itself is seen to go out.
            CinematicLayer dark = Glow("Lantern Dark", u, v, 0.17f, far, size, new Color(0.15f, 0.2f, 0.19f, 1f), 1f, additive: false);
            dark.opacity = Keys((0f, 0f), (out_ - 0.01f, 0f), (out_ + 0.02f, 1f), (1f, 1f));
            shot.layers.Insert(at++, dark);
            shot.layers.Insert(at++, light);
        }

        shot.layers.Add(Mist("Mist", 0.5f, new Vector2(-0.012f, 0f), 0.35f));
        shot.layers.Add(Vignette(1f));
        shot.sounds.Add(Bed(Wind, 0f, 0.2f, 1.2f));
        for (int i = 0; i < 5; i++)
        {
            shot.sounds.Add(Bed(Sfx + $"Safe Haven Footsteps/Footstep Forest {1 + i * 2 % 8}.wav", 0.9f + i * 0.62f, 0.2f - i * 0.03f, 0.05f, loop: false));
        }

        return shot;
    }

    // 9. A hard cut to daylight: four more, on the road. The nearest is the player.
    private static CinematicShot FourMore()
    {
        var from = new CinematicPose(-0.02f, 0f, 0f);
        var to = new CinematicPose(0.02f, 0f, 0.04f);
        CinematicShot shot = Shot("Four More", CinematicTransition.Cut, 0f, from, to, 1.6f,
            ("s09a", "So four more came looking.", 1.1f),
            ("s09b", "One of them was you.", 1f));
        Painting(shot, "s09_four_more");
        shot.layers.Add(Specks("Pollen", -0.2f, new Color(1f, 0.97f, 0.8f, 0.8f), additive: true, new CinematicMotes
        {
            count = 45, size = new Vector2(0.002f, 0.006f), velocity = new Vector2(0.02f, 0.006f), wander = 0.03f, wanderSeconds = 4f,
            twinkle = 0.7f, area = new Rect(0f, 0f, 1f, 0.7f), edgeFade = 0.2f,
        }));
        shot.layers.Add(Vignette(0.4f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 2.wav", 0.3f, 0.24f, 0.2f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 4.wav", 2.6f, 0.2f, 0.2f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 1.wav", 4.7f, 0.18f, 0.2f, loop: false));
        return shot;
    }

    // 10. The morning fire at the forest's edge, where the game begins.
    private static CinematicShot MorningFire()
    {
        var from = new CinematicPose(0f, -0.02f, 0.085f);
        var to = new CinematicPose(0f, 0.008f, 0f);
        CinematicShot shot = Shot("The Morning Fire", CinematicTransition.Dissolve, 1.8f, from, to, 3f,
            ("s10a", "That is the old story.", 1.1f),
            ("s10b", "Yours begins here: a bright morning, a small fire, and the trees waiting.", 1f));
        shot.easeCamera = true;
        float size = Painting(shot, "s10_morning_fire");
        shot.layers.Insert(3, Glow("Firelight", 0.492f, 0.23f, 0.5f, 0f, size, Firelight, 0.35f, flicker: 0.3f));
        shot.layers.Add(Specks("Sparks", 0f, Ember, additive: true, new CinematicMotes
        {
            count = 14, size = new Vector2(0.002f, 0.005f), velocity = new Vector2(0.006f, 0.08f), wander = 0.015f, wanderSeconds = 2f,
            twinkle = 0.5f, area = new Rect(0.45f, 0.2f, 0.1f, 0.35f), edgeFade = 0.3f,
        }));
        shot.layers.Add(Mist("Morning Mist", 1.5f, new Vector2(0.008f, 0f), 0.22f));
        shot.layers.Add(Vignette(0.5f));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 3.wav", 0.6f, 0.2f, 0.2f, loop: false));
        shot.sounds.Add(Bed(Sfx + "Safe Haven Birds/Bird Chirp 1.wav", 4.2f, 0.16f, 0.2f, loop: false));
        return shot;
    }

    // ----- The main menu -----

    // Sets the cinematic on the menu's MainMenuView. The menu scene is opened beside whatever is open, saved and closed again.
    private static string WireMainMenu(CinematicDefinition definition)
    {
        Scene menu = SceneManager.GetSceneByPath(MenuScene);
        bool opened = !menu.isLoaded;
        if (opened)
        {
            menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
        }

        try
        {
            MainMenuView view = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuView>(true)).FirstOrDefault();
            if (view == null)
            {
                return "No MainMenuView in the menu scene: not wired.";
            }

            var serialized = new SerializedObject(view);
            SerializedProperty field = serialized.FindProperty("openingCinematic");
            if (field.objectReferenceValue == definition)
            {
                return "The main menu already opens with it.";
            }

            field.objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            return "Set as the main menu's opening cinematic.";
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(menu, true);
            }
        }
    }
}
