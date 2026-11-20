using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Step three of the combat setup: what each family of weapons looks like. Every family has its own clips (the Stylized Character
/// base pack, Assets/StylizedCharacter) and a weapon art, and what it inherits from the attack it was split from is the damage and
/// the cadence: a new swing lands and recovers exactly when the old one did, the clip is retimed to fit, so damage per second and
/// gear score stay as they were. Monsters with a humanoid body get their own strikes and a hit reaction here too, played so the blow
/// lands at the attack's Hit Delay.
/// </summary>
public static partial class CombatAnimationSetup
{
    private const string StylizedPack = "Assets/StylizedCharacter/Animations/Character/Human/Male/A_Hu_M_BasePack.fbx";
    // The blade follows the hand: seen on the rig, a long weapon comes level with the target about this long after the hand's drive.
    private const float BladeLag = 0.09f;

    // Animator state -> clip in the Stylized pack.
    private static readonly (string state, string clip)[] StylizedStates =
    {
        ("1H Chop", "Combat_1H_Attack"),
        ("1H Slash", "Combat_1H_Attack01"),
        ("1H Stab", "Combat_1H_Attack02"),
        ("1H Thrust", "Combat_1H_Attack_Off01"),
        ("1H Spin", "Combat_1H_AttackSpecial"),
        ("1H Low Cut", "Combat_1H_AttackSpecial01"),
        ("2H Overhead", "Combat_2H_Attack"),
        ("2H Sweep", "Combat_2H_Attack01"),
        ("2H Drive", "Combat_2H_Attack02"),
        ("2H Slam", "Combat_2H_AttackSpecial"),
        ("2H Wide", "Combat_2H_Attack_Base"),
        ("2H Cleave", "Combat_2H_Attack_Base01"),
        ("Whirlwind", "Combat_Whirlwind"),
        ("Pole Lunge", "Combat_2HL_Attack"),
        ("Pole Rise", "Combat_2HL_Attack01"),
        ("Pole Slam", "Combat_2HL_AttackSpecial"),
    };

    public static AnimationClip Stylized(string name) => AssetDatabase.LoadAllAssetsAtPath(StylizedPack).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

    private static Motion StylizedState(string state) => Stylized(StylizedStates.First(s => s.state == state).clip);

    // The spin is one quick turn of the body on a loop: three turns, the swing's second landing the blow.
    private const string WhirlwindState = "Whirlwind";
    // 4.5 turns last the axe art's whole recovery (3 left the player standing locked for 0.4 s; audit 2026-11-20).
    private const float WhirlwindTurns = 4.5f;

    private static IEnumerable<Action> StylizedActions() =>
        StylizedStates.Select(s => new Action(s.state, Stylized(s.clip), "Empty", s.state == WhirlwindState ? WhirlwindTurns : 1f));

    private static AttackSpec Swing(string asset, string from, string state, float impact, float strikeClip = -1f, float strikeSpeed = 1.35f, float landsBy = 0f) => new AttackSpec
    {
        Asset = $"{ComboFolder}/{asset}.asset", From = from, State = state, Clip = StylizedState(state), Impact = impact,
        StrikeClip = strikeClip, StrikeSpeed = strikeSpeed, ContactLag = BladeLag, LandsBy = landsBy,
    };

    // A rush attack: the quick lunge out of a dodge or on the run. Its own numbers; the dodge's cooldown or the run-up keeps it in check.
    private static AttackSpec Rush(string asset, string state, Motion clip, float ratio, float reach, float arc, float lands, float recovers, float lunge, float impact, float lag) => new AttackSpec
    {
        Asset = $"{ComboFolder}/{asset}.asset", Template = "SwordSlash", State = state, Clip = clip, Ratio = ratio, Range = reach, Arc = arc,
        Targets = 2, Poise = 14f, Lunge = lunge, Impact = impact, LandsAt = lands, RecoversAt = recovers, StrikeClip = 0.08f, StrikeSpeed = 1.5f, ContactLag = lag,
    };

    // A weapon art: its own numbers, paid for with Focus.
    private static AttackSpec Art(string asset, string state, float ratio, float reach, float arc, int targets, float lands, float recovers, float impact, Color slash) => new AttackSpec
    {
        Asset = $"{ComboFolder}/{asset}.asset", Template = "SwordSlash", State = state, Clip = StylizedState(state), Ratio = ratio, Range = reach, Arc = arc,
        Targets = targets, Poise = 45f, Lunge = 0.8f, Impact = impact, Slash = slash, LandsAt = lands, RecoversAt = recovers, StrikeClip = 0.14f, StrikeSpeed = 1.7f,
        ContactLag = state == WhirlwindState ? 0f : BladeLag, Looped = state == WhirlwindState,
    };

    private static readonly Color ArtSlash = new Color(1f, 0.62f, 0.3f, 0.95f);

    // No clip that leaves the ground for a heavy swing: it waits on the planted upper body, and the leap would have nowhere to start.
    private static List<AttackSpec> MovesetSpecs()
    {
        return new List<AttackSpec>
        {
            // One-handed axes and clubs: a slash, a stab, and the chop they were made for.
            Swing("Chop A", "SwordSlash", "1H Slash", 1.1f),
            Swing("Chop B", "Sword Slash B", "1H Stab", 1.1f),
            Swing("Chop C", "Sword Finisher", "1H Chop", 1.25f, 0.13f, 1.5f, 0.58f),
            Swing("Chop Heavy", "Sword Heavy", "1H Low Cut", 1.2f, 0.12f, 1.7f),
            // The dagger: quick thrusts, a slash between them, a lunging stab to end on. It keeps its own numbers.
            Swing("Dagger Stab A", "Dagger Stab A", "1H Thrust", 0.65f),
            Swing("Dagger Stab B", "Dagger Stab B", "1H Slash", 0.65f),
            Swing("Dagger Stab C", "Dagger Stab C", "1H Stab", 0.8f, 0.1f, 1.5f),
            Swing("Dagger Heavy", "Sword Heavy", "1H Low Cut", 0.8f, 0.12f, 1.7f),
            // Greataxes: a wide sweep and a cleave; the overhead for holding on.
            Swing("Axe Sweep", "GreataxeSweep", "2H Wide", 1.3f),
            Swing("Axe Finisher", "Axe Finisher", "2H Cleave", 1.4f, 0.14f, 1.6f, 0.64f),
            Swing("Axe Heavy", "Axe Heavy", "2H Overhead", 1.5f, 0.15f, 1.8f),
            // Greatswords: a flat sweep and a wide cut; a driving thrust for holding on.
            Swing("Greatsword A", "GreataxeSweep", "2H Sweep", 1.2f),
            Swing("Greatsword B", "Axe Finisher", "2H Wide", 1.3f, 0.14f, 1.6f, 0.68f),
            Swing("Greatsword Heavy", "Axe Heavy", "2H Drive", 1.45f, 0.15f, 1.8f),
            // Hammers: straight down, then a flat swing round; the overhead for holding on. Their art is the leap.
            Swing("Hammer A", "GreataxeSweep", "2H Cleave", 1.45f, 0.12f, 1.5f),
            Swing("Hammer B", "Axe Finisher", "2H Sweep", 1.6f, 0.14f, 1.7f),
            Swing("Hammer Heavy", "Hammer Heavy", "2H Overhead", 1.8f, 0.16f, 1.9f),
            // Spears: two thrusts, the deep lunge; the rising cut for holding on. (The pack's other 2HL swings lead with the butt end.)
            Swing("Spear Lunge", "Spear Lunge", "Pole Lunge", 1.05f, 0.12f, 1.5f),
            Swing("Spear Heavy", "Spear Heavy", "Pole Rise", 1.3f, 0.15f, 1.8f),
            // Weapon arts: the leaps are here, where nothing holds them.
            Art("Sword Art", "1H Spin", 2.4f, 2.8f, 200f, 4, 0.5f, 1f, 1.35f, ArtSlash),
            Art("Dagger Art", "1H Spin", 2.6f, 2.2f, 200f, 3, 0.4f, 0.85f, 1f, ArtSlash),
            Art("Chop Art", "1H Low Cut", 2.6f, 2.6f, 150f, 3, 0.45f, 1.1f, 1.5f, ArtSlash),
            Art("Axe Art", "Whirlwind", 1.9f, 3f, 360f, 6, 0.55f, 1.2f, 1.5f, ArtSlash),
            Art("Greatsword Art", "2H Slam", 2.6f, 3.1f, 150f, 4, 0.62f, 1.45f, 1.7f, ArtSlash),
            Art("Hammer Art", "2H Slam", 2.5f, 3.2f, 360f, 6, 0.64f, 1.6f, 2f, ArtSlash),
            Art("Spear Art", "Pole Slam", 2.3f, 3.6f, 170f, 4, 0.58f, 1.25f, 1.3f, ArtSlash),
            // Rush attacks: quick, and they carry the movement into the blow.
            Rush("Sword Rush", "Sword Counter", Clips()["Sword_Dash"], 1.2f, 2.3f, 90f, 0.28f, 0.62f, 2f, 1.05f, 0f),
            Rush("Dagger Rush", "1H Thrust", StylizedState("1H Thrust"), 1.2f, 1.9f, 70f, 0.2f, 0.5f, 1.8f, 0.7f, BladeLag),
            Rush("Chop Rush", "1H Stab", StylizedState("1H Stab"), 1.2f, 2.2f, 90f, 0.26f, 0.6f, 1.9f, 1.15f, BladeLag),
            Rush("Axe Rush", "2H Sweep", StylizedState("2H Sweep"), 1.2f, 2.7f, 130f, 0.36f, 0.88f, 1.9f, 1.35f, BladeLag),
            Rush("Greatsword Rush", "2H Drive", StylizedState("2H Drive"), 1.2f, 2.9f, 70f, 0.36f, 0.88f, 2.3f, 1.3f, BladeLag),
            Rush("Hammer Rush", "2H Cleave", StylizedState("2H Cleave"), 1.2f, 2.6f, 100f, 0.38f, 0.92f, 1.8f, 1.5f, BladeLag),
            Rush("Spear Rush", "Pole Lunge", StylizedState("Pole Lunge"), 1.2f, 3.3f, 45f, 0.32f, 0.78f, 2.4f, 1f, BladeLag),
            Rush("Fists Rush", "Punch Cross", Ual1("Punch_Cross"), 1.2f, 1.6f, 90f, 0.18f, 0.44f, 1.6f, 0.75f, 0f),
        };
    }

    // The attacks that keep the clip they already had still get the swing's new shape and their weight.
    private static void ShapeExistingAttacks(Dictionary<string, AttackDefinition> made)
    {
        var weights = new Dictionary<string, float>
        {
            ["SwordSlash"] = 1f, ["Sword Slash B"] = 1f, ["Sword Finisher"] = 1.15f, ["Sword Heavy"] = 1.3f, ["Sword Counter"] = 1.2f,
            ["FistsStrike"] = 0.65f, ["Fists Cross"] = 0.7f, ["Fists Hook"] = 0.85f, ["GreataxeSweep"] = 1.3f, ["SpearThrust"] = 0.9f, ["ShieldBash"] = 1.1f,
        };
        foreach (KeyValuePair<string, float> weight in weights)
        {
            if (made.TryGetValue(weight.Key, out AttackDefinition attack))
            {
                var data = new SerializedObject(attack);
                data.FindProperty("impact").floatValue = weight.Value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void AssignMovesets(Dictionary<string, AttackDefinition> made)
    {
        void Family(string[] weapons, string[] chain, string heavy, string counter, string art, float focus, string rush, float secondHand = 0.24f)
        {
            foreach (string weapon in weapons)
            {
                Weapon(weapon, chain, heavy, counter, null, made);
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponFolder}/{weapon}.asset");
                if (definition != null)
                {
                    var data = new SerializedObject(definition);
                    data.FindProperty("weaponArt").objectReferenceValue = art != null ? made[art] : null;
                    data.FindProperty("weaponArtFocus").floatValue = focus;
                    data.FindProperty("rushAttack").objectReferenceValue = made[rush];
                    data.FindProperty("secondHandDistance").floatValue = secondHand;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        Family(new[] { "Fists" }, new[] { "FistsStrike", "Fists Cross", "Fists Hook" }, null, null, null, 0f, "Fists Rush");
        Family(new[] { "IronSword", "CopperSword", "GraveBlade", "MoonsilverBlade" }, new[] { "SwordSlash", "Sword Slash B", "Sword Finisher" }, "Sword Heavy", "Sword Counter", "Sword Art", 30f, "Sword Rush");
        Family(new[] { "FangDagger" }, new[] { "Dagger Stab A", "Dagger Stab B", "Dagger Stab C" }, "Dagger Heavy", "Sword Counter", "Dagger Art", 25f, "Dagger Rush");
        Family(new[] { "CopperHandAxe", "WoodsmansAxe", "KnottedClub" }, new[] { "Chop A", "Chop B", "Chop C" }, "Chop Heavy", "Sword Counter", "Chop Art", 30f, "Chop Rush");
        Family(new[] { "StoneAxe", "Greataxe" }, new[] { "Axe Sweep", "Axe Finisher" }, "Axe Heavy", null, "Axe Art", 40f, "Axe Rush", 0.34f);
        Family(new[] { "IronGreatsword" }, new[] { "Greatsword A", "Greatsword B" }, "Greatsword Heavy", null, "Greatsword Art", 35f, "Greatsword Rush", 0.17f);
        Family(new[] { "IronWarhammer", "TrollstoneMaul" }, new[] { "Hammer A", "Hammer B" }, "Hammer Heavy", null, "Hammer Art", 40f, "Hammer Rush", 0.34f);
        Family(new[] { "Spear", "CopperSpear", "FireHardenedSpear" }, new[] { "SpearThrust", "Spear Thrust B", "Spear Lunge" }, "Spear Heavy", null, "Spear Art", 30f, "Spear Rush", 0.45f);
    }

    // ----- Monsters -----

    // Animator state on the humanoid monsters' controller -> clip.
    private static readonly (string state, string clip)[] MonsterStates =
    {
        ("Strike Overhead", "Combat_2H_Attack"),
        ("Strike Wide", "Combat_2H_Attack_Base"),
        ("Strike Chop", "Combat_1H_Attack"),
        ("Strike Lunge", "Combat_1H_Attack02"),
        ("Strike Claw", "Combat_Unarmed_Attack"),
        ("Roar", "Emote_Roar"),
        ("Hop", "Combat_1H_Dodge"),
    };

    // Attack -> the state it plays on a humanoid body. Monsters built from rigid parts use none of them: their pose is procedural.
    private static readonly Dictionary<string, string> MonsterAttackStates = new Dictionary<string, string>
    {
        ["DraugCleave"] = "Strike Chop", ["DraugBackswing"] = "Strike Wide", ["DraugLunge"] = "Strike Lunge", ["DraugWarCry"] = "Roar",
        ["GuardianSlam"] = "Strike Chop", ["GuardianGroundSlam"] = "Strike Overhead", ["GuardianCallRoots"] = "Roar",
        ["TrollSweep"] = "Strike Wide", ["TrollStomp"] = "Strike Overhead", ["OldTrollCharge"] = "Strike Lunge", ["OldTrollCallKin"] = "Roar",
        ["MonsterStrike"] = "Strike Claw",
    };

    private const float MonsterHitPlayback = 1.7f;

    private static void BuildMonsterStates(AnimatorController controller)
    {
        Parameter(controller, ActionSpeed, AnimatorControllerParameterType.Float, 1f);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = Find(machine, "Locomotion");
        int row = 0;
        foreach ((string state, string clip) in MonsterStates)
        {
            AnimationClip motion = Stylized(clip);
            if (motion == null)
            {
                Debug.LogWarning($"Combat animations: no clip {clip} for the monsters' {state}.");
                continue;
            }

            AnimatorState made = State(machine, state, motion, new Vector3(800f, 60f * row++));
            made.speedParameterActive = true;
            made.speedParameter = ActionSpeed;
            ClearTransitions(made);
            Exit(made, locomotion, 0.92f, 0.15f);
        }

        AnimatorState attack = Find(machine, "Attack");
        if (attack != null)
        {
            attack.speedParameterActive = true;
            attack.speedParameter = ActionSpeed;
        }

        // A flinch from an ordinary blow, quicker than the clip as authored so it never holds up the fight.
        AnimatorState hit = State(machine, "Hit", Stylized("Combat_1H_Hit"), new Vector3(800f, 60f * row));
        hit.speed = MonsterHitPlayback;
        ClearTransitions(hit);
        Exit(hit, locomotion, 0.7f, 0.15f);

        foreach ((string attackName, string stateName) in MonsterAttackStates)
        {
            string guid = AssetDatabase.FindAssets($"{attackName} t:AttackDefinition").FirstOrDefault(g =>
                System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == attackName);
            AnimationClip clip = Stylized(MonsterStates.First(s => s.state == stateName).clip);
            if (guid == null || clip == null)
            {
                continue;
            }

            var definition = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            float contact = stateName == "Roar" ? clip.length * 0.3f : Mathf.Min(DrivenContact(clip) + BladeLag, clip.length);
            var data = new SerializedObject(definition);
            data.FindProperty("animationState").stringValue = stateName;
            // Monsters' timing is their balance: the clip is fitted to it, never the other way round.
            data.FindProperty("animationSpeed").floatValue = Mathf.Clamp(contact / Mathf.Max(0.05f, definition.HitDelay), 0.25f, 3f);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ----- Monsters' bodies -----

    private static readonly Dictionary<string, (bool bleeds, Color blood, bool ragdoll)> MonsterBodies = new Dictionary<string, (bool, Color, bool)>
    {
        ["MarshLight"] = (false, Color.clear, false),
        ["Mistwing"] = (true, new Color(0.45f, 0.45f, 0.55f, 0.8f), true),
        ["Draug"] = (true, new Color(0.03f, 0.025f, 0.03f, 0.95f), true),
        ["BarrowShieldbearer"] = (true, new Color(0.03f, 0.025f, 0.03f, 0.95f), true),
        ["RootGuardian"] = (true, new Color(0.16f, 0.2f, 0.05f, 0.95f), true),
        ["Rootling"] = (true, new Color(0.16f, 0.2f, 0.05f, 0.95f), true),
        ["MossShaman"] = (true, new Color(0.14f, 0.18f, 0.05f, 0.95f), true),
        ["BogSpitter"] = (true, new Color(0.3f, 0.33f, 0.05f, 0.95f), true),
    };

    private static void SetMonsterBodies()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (!MonsterBodies.TryGetValue(monster.name, out (bool bleeds, Color blood, bool ragdoll) body))
            {
                continue;
            }

            var data = new SerializedObject(monster);
            data.FindProperty("bleeds").boolValue = body.bleeds;
            data.FindProperty("bloodColor").colorValue = body.blood;
            data.FindProperty("ragdollOnDeath").boolValue = body.ragdoll;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>When a swing connects: its hand's fastest moment past the very start (the first frames snap from the ready pose).</summary>
    public static float Contact(AnimationClip clip, HumanBodyBones hand)
    {
        List<float> contacts = ContactTimes(clip, hand);
        foreach (float time in contacts)
        {
            if (time >= 0.12f)
            {
                return time;
            }
        }

        return contacts.DefaultIfEmpty(0.3f).First();
    }
}
