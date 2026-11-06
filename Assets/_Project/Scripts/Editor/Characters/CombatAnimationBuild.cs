using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Step two of the combat setup: the animator states the combat code crossfades into by name, the combo, heavy and
/// counter attacks with timings measured from their clips, each weapon's chain, the player prefab's guard and the
/// Combat Technique settings asset. Everything is idempotent: running it again updates what it made and keeps
/// designers' other changes (damage numbers on existing attacks are only set when the asset is first created).
/// </summary>
public static partial class CombatAnimationSetup
{
    public const string PlayerControllerPath = "Assets/_Project/Animation/Controllers/PlayerAnimator.controller";
    public const string MonsterControllerPath = "Assets/_Project/Animation/Controllers/MonsterHumanoid.controller";
    public const string AttackFolder = "Assets/_Project/Data/Combat/Attacks";
    public const string ComboFolder = AttackFolder + "/Combos";
    public const string WeaponFolder = "Assets/_Project/Data/Combat/Weapons";
    public const string SettingsPath = "Assets/_Project/Resources/CombatTechniqueSettings.asset";
    private const string Ual1Path = Library1Path;
    private const string ActionSpeed = "ActionSpeed";
    // The first slam of the heavy combo, cut out as its own clip.
    private const string HeavyClip = "Sword_Heavy";
    private const float HeavyClipSeconds = 1.05f;

    private static readonly Color LightSlash = new Color(1f, 0.95f, 0.85f, 0.75f);
    private static readonly Color FinisherSlash = new Color(1f, 0.8f, 0.45f, 0.9f);
    // Fists: a punch draws no blade's arc; it lands with Punch Impact (the weapon's Hit Effect).
    private static readonly Color NoSlash = new Color(1f, 1f, 1f, 0f);
    private static readonly Color HeavySlash = new Color(1f, 0.55f, 0.25f, 0.95f);
    private static readonly Color CounterSlash = new Color(0.55f, 0.85f, 1f, 0.95f);

    [MenuItem("Safe Haven/Setup/Characters/Set Up Combat Animations", false, 200)]
    public static void SetUpAll()
    {
        ConfigureImport();
        AddHeavyClip();
        BuildPlayerController();
        Debug.Log(BuildBowLayer());
        // Only when missing: Build Character Animators has since put the modular pack's stealth clips in it.
        if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath).layers.Any(layer => layer.name == "Sneak"))
        {
            BuildSneakLayer();
        }

        BuildMonsterController();
        int attacks = BuildAttacksAndWeapons();
        EnsureSettings();
        AddGuardToPlayer();
        FitDodges();
        AddShoulderBarge();
        SetSkillFocusCosts();
        AssetDatabase.SaveAssets();
        Debug.Log($"Combat animations set up: player and monster states, {attacks} attack(s), weapon chains, the player's guard.");
    }

    private static void AddHeavyClip()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(LibraryPath);
        List<ModelImporterClipAnimation> clips = importer.clipAnimations.ToList();
        if (clips.Any(c => c.name == HeavyClip))
        {
            return;
        }

        ModelImporterClipAnimation combo = clips.First(c => c.name == "Sword_Heavy_Combo");
        float fps = combo.lastFrame / Clips()["Sword_Heavy_Combo"].length;
        var heavy = new ModelImporterClipAnimation
        {
            name = HeavyClip, takeName = combo.takeName, firstFrame = 0f, lastFrame = Mathf.Round(HeavyClipSeconds * fps),
            keepOriginalPositionY = true,
        };
        clips.Add(heavy);
        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
    }

    public static AnimationClip Ual1(string name) => AssetDatabase.LoadAllAssetsAtPath(Ual1Path).OfType<AnimationClip>()
        .FirstOrDefault(c => c.name == name || c.name == "Armature|" + name);

    // ----- Player controller -----

    /// <summary>A combat action: a state, its clip and the state it flows into when nothing interrupts it.</summary>
    private readonly struct Action
    {
        public readonly string State;
        public readonly Motion Clip;
        public readonly string Then;
        // Times through the clip before it flows on: above 1 for a clip that loops (a whirlwind's spin).
        public readonly float ExitTime;

        public Action(string state, Motion clip, string then = "Empty", float exitTime = 1f)
        {
            State = state;
            Clip = clip;
            Then = then;
            ExitTime = exitTime;
        }
    }

    private static List<Action> PlayerActions()
    {
        Dictionary<string, AnimationClip> ual = Clips();
        var actions = new List<Action>
        {
            new Action("Sword A", ual["Sword_Regular_A"], "Sword A Recover"),
            new Action("Sword A Recover", ual["Sword_Regular_A_Rec"]),
            new Action("Sword B", ual["Sword_Regular_B"], "Sword B Recover"),
            new Action("Sword B Recover", ual["Sword_Regular_B_Rec"]),
            new Action("Sword Finisher", ual["Sword_Regular_C"]),
            new Action("Sword Heavy", ual[HeavyClip]),
            new Action("Sword Counter", ual["Sword_Dash"]),
            new Action("Punch Jab", Ual1("Punch_Jab")),
            new Action("Punch Cross", Ual1("Punch_Cross")),
            new Action("Punch Hook", ual["Melee_Hook"], "Punch Hook Recover"),
            new Action("Punch Hook Recover", ual["Melee_Hook_Rec"]),
            new Action("Attack Two Handed", StateClip("Attack Two Handed")),
            new Action("Attack Polearm", StateClip("Attack Polearm")),
            new Action("Shield Bash", StateClip("Shield Bash")),
        };
        actions.AddRange(StylizedActions());
        return actions;
    }

    public const string FullBodyLayer = "Combat Full Body";

    /// <summary>
    /// Where in the recovery clip to blend in when a chain swing isn't followed up. Each "_Rec" clip is the whole swing
    /// plus its return to guard, so it is entered at its own contact plus the time the chain swing has played past its
    /// contact: the pose matches and the swing isn't played twice.
    /// </summary>
    public static float RecoverOffset(AnimationClip swing, AnimationClip recover)
    {
        float swingContact = ContactTimes(swing).DefaultIfEmpty(swing.length * 0.5f).First();
        // The recovery clip's own fastest moment can be the return to guard, so take its contact nearest the swing's.
        float recoverContact = ContactTimes(recover, HumanBodyBones.RightHand, 4).DefaultIfEmpty(recover.length * 0.3f)
            .OrderBy(t => Mathf.Abs(t - swingContact)).First();
        return Mathf.Clamp01((recoverContact + swing.length - swingContact) / recover.length);
    }

    // A layer with no mask for swings made standing still, so the legs step and the weight shifts with the blow.
    private static void EnsureFullBodyLayer(AnimatorController controller)
    {
        if (controller.layers.Any(l => l.name == FullBodyLayer))
        {
            return;
        }

        var machine = new AnimatorStateMachine { name = FullBodyLayer, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(machine, controller);
        machine.AddState("Empty", new Vector3(300f, 0f));
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = FullBodyLayer, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = machine,
        });
    }

    public static void BuildPlayerController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        Dictionary<string, AnimationClip> ual = Clips();
        Parameter(controller, ActionSpeed, AnimatorControllerParameterType.Float, 1f);
        Parameter(controller, "Guarding", AnimatorControllerParameterType.Bool, 0f);
        Parameter(controller, "HasShield", AnimatorControllerParameterType.Bool, 0f);
        List<Action> actions = PlayerActions();
        EnsureFullBodyLayer(controller);

        foreach (AnimatorControllerLayer layer in controller.layers.Where(l => l.name == "Upper Body" || l.name == "Attack Arms" || l.name == FullBodyLayer))
        {
            AnimatorStateMachine machine = layer.stateMachine;
            int row = 0;
            foreach (Action action in actions)
            {
                if (action.Clip == null)
                {
                    Debug.LogWarning($"Combat animations: no clip for {action.State}.");
                    continue;
                }

                AnimatorState state = State(machine, action.State, action.Clip, new Vector3(900f, 60f * row++));
                state.speedParameterActive = true;
                state.speedParameter = ActionSpeed;
                ClearTransitions(state);
                Action then = actions.FirstOrDefault(a => a.State == action.Then);
                float offset = then.Clip is AnimationClip recover && action.Clip is AnimationClip swing ? RecoverOffset(swing, recover) : 0f;
                AnimatorStateTransition exit = Exit(state, Find(machine, action.Then), action.ExitTime, 0.12f);
                if (exit != null)
                {
                    exit.offset = offset;
                }
            }

            // The older single attacks are crossfaded into by name too, so they follow the attack's speed.
            foreach (string old in new[] { "Attack Two Handed", "Attack Polearm", "Attack One Handed Left", "Attack One Handed Right", "Shield Bash" })
            {
                AnimatorState state = Find(machine, old);
                if (state != null)
                {
                    state.speedParameterActive = true;
                    state.speedParameter = ActionSpeed;
                }
            }

            if (layer.name == FullBodyLayer)
            {
                // The dodges, one per gear weight (PlayerMovementSettings > Equipment load).
                foreach ((string name, AnimationClip clip) in DodgeClips())
                {
                    AnimatorState dodge = State(machine, name, clip, new Vector3(1200f, 60f * row++));
                    dodge.speedParameterActive = true;
                    dodge.speedParameter = ActionSpeed;
                    ClearTransitions(dodge);
                    Exit(dodge, Find(machine, "Empty"), 0.92f, 0.15f);
                }
            }

            if (layer.name != "Upper Body")
            {
                continue;
            }

            AnimatorState empty = Find(machine, "Empty");
            AnimatorState consume = State(machine, "Consume", ual["Consume"], new Vector3(1200f, 300f));
            ClearTransitions(consume);
            Exit(consume, empty, 0.95f, 0.2f);
            AnimatorState guard = State(machine, "Guard", ual["Sword_Block"], new Vector3(1200f, 0f));
            AnimatorState guardShield = State(machine, "Guard Shield", ual["Idle_Shield_Loop"], new Vector3(1200f, 60f));
            AnimatorState parry = State(machine, "Parry", ual["Shield_OneShot"], new Vector3(1200f, 120f));
            AnimatorState broken = State(machine, "Guard Break", ual["Idle_Shield_Break"], new Vector3(1200f, 180f));
            AnimatorState knockback = State(machine, "Knockback", ual["Hit_Knockback"], new Vector3(1200f, 240f));
            foreach (AnimatorState state in new[] { guard, guardShield, parry, broken, knockback })
            {
                ClearTransitions(state);
            }

            RemoveTransitionsTo(empty, guard, guardShield);
            When(empty, guard, 0.1f, ("Guarding", true), ("HasShield", false));
            When(empty, guardShield, 0.1f, ("Guarding", true), ("HasShield", true));
            When(guard, empty, 0.15f, ("Guarding", false));
            When(guardShield, empty, 0.15f, ("Guarding", false));
            When(guard, guardShield, 0.1f, ("HasShield", true));
            When(guardShield, guard, 0.1f, ("HasShield", false));
            Exit(parry, guard, 0.85f, 0.12f, ("Guarding", true), ("HasShield", false));
            Exit(parry, guardShield, 0.85f, 0.12f, ("Guarding", true), ("HasShield", true));
            Exit(parry, empty, 0.9f, 0.15f, ("Guarding", false));
            Exit(broken, empty, 0.95f, 0.15f);
            Exit(knockback, empty, 0.9f, 0.15f);
        }

        EditorUtility.SetDirty(controller);
    }

    public static void BuildMonsterController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MonsterControllerPath);
        if (controller == null)
        {
            return;
        }

        Parameter(controller, "Stagger", AnimatorControllerParameterType.Trigger, 0f);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState stagger = State(machine, "Stagger", Clips()["Hit_Knockback"], new Vector3(500f, 200f));
        ClearTransitions(stagger);
        AnimatorState locomotion = Find(machine, "Locomotion");
        Exit(stagger, locomotion, 0.95f, 0.15f);
        if (!machine.anyStateTransitions.Any(t => t.destinationState == stagger))
        {
            AnimatorStateTransition any = machine.AddAnyStateTransition(stagger);
            any.AddCondition(AnimatorConditionMode.If, 0f, "Stagger");
            any.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            any.duration = 0.08f;
            any.canTransitionToSelf = false;
        }

        BuildMonsterStates(controller);
        EditorUtility.SetDirty(controller);
    }

    private static void Parameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float value)
    {
        if (controller.parameters.Any(p => p.name == name))
        {
            return;
        }

        controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = value });
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string name) =>
        machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    private static AnimatorState State(AnimatorStateMachine machine, string name, Motion clip, Vector3 position)
    {
        AnimatorState state = Find(machine, name) ?? machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static void ClearTransitions(AnimatorState state)
    {
        foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    private static void RemoveTransitionsTo(AnimatorState from, params AnimatorState[] targets)
    {
        foreach (AnimatorStateTransition transition in from.transitions.Where(t => targets.Contains(t.destinationState)).ToArray())
        {
            from.RemoveTransition(transition);
        }
    }

    private static AnimatorStateTransition Exit(AnimatorState from, AnimatorState to, float exitTime, float duration, params (string name, bool value)[] conditions)
    {
        if (to == null)
        {
            return null;
        }

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        foreach ((string name, bool value) in conditions)
        {
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);
        }

        return transition;
    }

    private static void When(AnimatorState from, AnimatorState to, float duration, params (string name, bool value)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        foreach ((string name, bool value) in conditions)
        {
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);
        }
    }

    // ----- Attacks and weapons -----

    /// <summary>How one attack is made: from which clip and animation state, at what speed, and its numbers when new.</summary>
    private sealed class AttackSpec
    {
        public string Asset;
        public string Template;
        public string State;
        public Motion Clip;
        // Wind-ups a little slower than the clips as authored, for weight; the strike keeps the clip's snap. Heavy swings
        // and finishers set their own, slower. (0.75 for every swing made the light chain feel late to answer.)
        public float Speed = 0.9f;
        public float FollowThrough = 1f;
        // Seconds after the contact the next swing may start; below 0 the clip's end (light chain swings).
        public float RecoverAfterContact = -1f;
        public float Ratio = 1f;
        public float Range;
        public float Arc;
        public int Targets = 3;
        public float Poise = 10f;
        public float Lunge = 0.6f;
        public Color Slash = LightSlash;
        public HumanBodyBones Hand = HumanBodyBones.RightHand;
        // The attack whose numbers and cadence this one takes over (it may be itself): it lands and recovers exactly when that one
        // did, and the clip is fitted to that, so swapping a clip changes no damage per second.
        public string From;
        // Or when it lands and recovers, in seconds (a weapon art, which has no attack before it).
        public float LandsAt;
        public float RecoversAt;
        // The strike: the last seconds of the clip before contact, and how fast they play. Below 0: chosen from the contact.
        public float StrikeClip = -1f;
        public float StrikeSpeed = 1.35f;
        // How much the blow weighs (AttackDefinition.Impact).
        public float Impact = 1f;
        // Seconds after the hand's drive that the weapon itself arrives.
        public float ContactLag;
        // A clip that loops for the whole swing (a whirlwind): it plays at its own speed, with no build-up.
        public bool Looped;
        // At the latest, seconds after it starts that the blow lands, however late the attack it takes its cadence from: the time saved
        // goes to the follow-through, so a finisher answers sooner and the chain still takes as long.
        public float LandsBy;
    }

    private static Motion StateClip(string state) =>
        AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath).layers.SelectMany(l => l.stateMachine.states)
            .Select(s => s.state).FirstOrDefault(s => s.name == state)?.motion;

    private static (string, AnimationClip)[] DodgeClips()
    {
        Dictionary<string, AnimationClip> ual = Clips();
        return new[] { ("Quickstep", ual["Slide_Start"]), ("Roll", Ual1("Roll")), ("Heavy Roll", ual["Shield_Dash"]) };
    }

    // A roll that reads as a roll: half a second, not a blink.
    private const float DodgeSeconds = 0.5f;

    // How much of each dodge clip is the dodge itself (the rest is getting back up), to fit it to the dash's time.
    private static readonly Dictionary<string, float> DodgeShare = new Dictionary<string, float>
    {
        ["Quickstep"] = 0.9f, ["Roll"] = 0.6f, ["Heavy Roll"] = 0.5f,
    };

    /// <summary>Fits the dodge animations to the dodges (speed so the clip's dodge part lasts the dash). Returns a line per style.</summary>
    public static string FitDodges()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/_Project/Data/Player/PlayerMovementSettings.asset");
        var data = new SerializedObject(settings);
        // How far and how long a dodge goes are designers' numbers: only its animation is fitted here.
        var report = new List<string>();
        foreach ((string state, AnimationClip clip) in DodgeClips())
        {
            string field = state == "Quickstep" ? "quickstep" : state == "Roll" ? "roll" : "heavyRoll";
            SerializedProperty shape = data.FindProperty(field);
            float duration = DodgeSeconds * shape.FindPropertyRelative("duration").floatValue;
            float speed = Mathf.Clamp(clip.length * DodgeShare[state] / duration, 0.6f, 2.5f);
            shape.FindPropertyRelative("animationState").stringValue = state;
            shape.FindPropertyRelative("animationSpeed").floatValue = speed;
            report.Add($"{state} {duration:0.00}s x{speed:0.00}");
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return string.Join(", ", report);
    }

    public static int BuildAttacksAndWeapons()
    {
        Directory(ComboFolder);
        Dictionary<string, AnimationClip> ual = Clips();
        Motion twoHanded = StateClip("Attack Two Handed");
        Motion polearm = StateClip("Attack Polearm");

        var specs = new List<AttackSpec>
        {
            // Sword: A, B, the finisher; a heavy slam; a dashing counter.
            new AttackSpec { Asset = AttackFolder + "/SwordSlash.asset", State = "Sword A", Clip = ual["Sword_Regular_A"], Ratio = 1f, Range = 2.2f, Arc = 110f, Poise = 10f },
            new AttackSpec { Asset = ComboFolder + "/Sword Slash B.asset", Template = "SwordSlash", State = "Sword B", Clip = ual["Sword_Regular_B"], Ratio = 1.1f, Range = 2.2f, Arc = 120f, Poise = 12f },
            new AttackSpec { Asset = ComboFolder + "/Sword Finisher.asset", Template = "SwordSlash", State = "Sword Finisher", Clip = ual["Sword_Regular_C"], Speed = 0.82f, RecoverAfterContact = 0.5f, Ratio = 1.6f, Range = 2.5f, Arc = 170f, Poise = 22f, Lunge = 0.6f, Slash = FinisherSlash },
            new AttackSpec { Asset = ComboFolder + "/Sword Heavy.asset", Template = "SwordSlash", State = "Sword Heavy", Clip = ual[HeavyClip], Speed = 0.7f, RecoverAfterContact = 0.5f, Ratio = 2.2f, Range = 2.6f, Arc = 120f, Poise = 40f, Lunge = 0.5f, Slash = HeavySlash },
            new AttackSpec { Asset = ComboFolder + "/Sword Counter.asset", Template = "SwordSlash", State = "Sword Counter", Clip = ual["Sword_Dash"], RecoverAfterContact = 0.45f, Ratio = 1.4f, Range = 2.4f, Arc = 100f, Poise = 30f, Lunge = 1.8f, Slash = CounterSlash },
            // Fists: jab, cross, hook.
            new AttackSpec { Asset = AttackFolder + "/FistsStrike.asset", State = "Punch Jab", Clip = Ual1("Punch_Jab"), Speed = 1f, RecoverAfterContact = 0.25f, Ratio = 1f, Range = 1.6f, Arc = 90f, Poise = 6f, Lunge = 0.4f, Slash = NoSlash },
            new AttackSpec { Asset = ComboFolder + "/Fists Cross.asset", Template = "FistsStrike", State = "Punch Cross", Clip = Ual1("Punch_Cross"), Speed = 1f, RecoverAfterContact = 0.25f, Ratio = 1.1f, Range = 1.6f, Arc = 90f, Poise = 8f, Lunge = 0.4f, Slash = NoSlash },
            new AttackSpec { Asset = ComboFolder + "/Fists Hook.asset", Template = "FistsStrike", State = "Punch Hook", Clip = ual["Melee_Hook"], Ratio = 1.5f, Range = 1.7f, Arc = 110f, Poise = 16f, Lunge = 0.6f, Slash = NoSlash },
            // Axes (two-handed): the sweep, a spinning finisher; the heavy slam.
            new AttackSpec { Asset = AttackFolder + "/GreataxeSweep.asset", State = "Attack Two Handed", Clip = twoHanded, RecoverAfterContact = 0.4f, Ratio = 1f, Range = 2.6f, Arc = 180f, Poise = 15f },
            new AttackSpec { Asset = ComboFolder + "/Axe Finisher.asset", Template = "GreataxeSweep", State = "Sword Finisher", Clip = ual["Sword_Regular_C"], Speed = 0.75f, RecoverAfterContact = 0.55f, Ratio = 1.6f, Range = 2.7f, Arc = 200f, Poise = 28f, Lunge = 0.8f, Slash = FinisherSlash },
            new AttackSpec { Asset = ComboFolder + "/Axe Heavy.asset", Template = "GreataxeSweep", State = "Sword Heavy", Clip = ual[HeavyClip], Speed = 0.6f, RecoverAfterContact = 0.55f, Ratio = 2.4f, Range = 2.8f, Arc = 130f, Poise = 50f, Lunge = 0.4f, Slash = HeavySlash },
            // Spear: two thrusts, a lunging finisher; the heavy slam.
            new AttackSpec { Asset = AttackFolder + "/SpearThrust.asset", State = "Attack Polearm", Clip = polearm, RecoverAfterContact = 0.35f, Ratio = 1f, Range = 3.2f, Arc = 40f, Targets = 2, Poise = 10f },
            new AttackSpec { Asset = ComboFolder + "/Spear Thrust B.asset", Template = "SpearThrust", State = "Attack Polearm", Clip = polearm, RecoverAfterContact = 0.35f, Ratio = 1f, Range = 3.2f, Arc = 40f, Targets = 2, Poise = 10f },
            new AttackSpec { Asset = ComboFolder + "/Spear Lunge.asset", Template = "SpearThrust", State = "Sword Counter", Clip = ual["Sword_Dash"], Speed = 0.82f, RecoverAfterContact = 0.45f, Ratio = 1.5f, Range = 3.4f, Arc = 45f, Targets = 3, Poise = 22f, Lunge = 1.6f, Slash = FinisherSlash },
            new AttackSpec { Asset = AttackFolder + "/ShieldBash.asset", State = "Shield Bash", Clip = StateClip("Shield Bash"), Hand = HumanBodyBones.LeftHand, RecoverAfterContact = 0.3f, Ratio = 0f, Range = 1.8f, Arc = 100f, Poise = 30f, Lunge = 0.8f },
            new AttackSpec { Asset = ComboFolder + "/Spear Heavy.asset", Template = "SpearThrust", State = "Sword Heavy", Clip = ual[HeavyClip], Speed = 0.7f, RecoverAfterContact = 0.5f, Ratio = 2.1f, Range = 3f, Arc = 90f, Targets = 3, Poise = 40f, Lunge = 0.4f, Slash = HeavySlash },
        };

        // A spec that goes on to get a clip of its own in the movesets below isn't made here, so it never has its timing set twice.
        List<AttackSpec> movesets = MovesetSpecs();
        specs.RemoveAll(spec => movesets.Any(other => other.Asset == spec.Asset));
        specs.AddRange(movesets);
        var made = new Dictionary<string, AttackDefinition>();
        foreach (AttackSpec spec in specs)
        {
            made[System.IO.Path.GetFileNameWithoutExtension(spec.Asset)] = MakeAttack(spec, made);
        }

        ShapeExistingAttacks(made);
        AssignMovesets(made);
        SetMonsterBodies();
        return made.Count;
    }

    // Finds an attack by asset name: one made in this run, else in the combos folder, else in the attacks folder.
    private static AttackDefinition NamedAttack(string name, Dictionary<string, AttackDefinition> made)
    {
        if (made.TryGetValue(name, out AttackDefinition known))
        {
            return known;
        }

        AttackDefinition combo = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{ComboFolder}/{name}.asset");
        return combo != null ? combo : AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}/{name}.asset");
    }

    private static AttackDefinition MakeAttack(AttackSpec spec, Dictionary<string, AttackDefinition> made)
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(spec.Asset);
        bool created = attack == null;
        AttackDefinition source = spec.From != null ? NamedAttack(spec.From, made) : null;
        if (spec.From != null && source == null)
        {
            Debug.LogError($"Combat animations: {spec.Asset} takes its numbers from {spec.From}, which doesn't exist.");
        }

        if (created)
        {
            AttackDefinition template = source != null ? source : AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}/{spec.Template}.asset");
            attack = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<AttackDefinition>();
            AssetDatabase.CreateAsset(attack, spec.Asset);
        }

        // Timing follows the clip, unless the cadence is the point (see From); damage, reach and weight are only set on new attacks
        // (designers own them after).
        var clip = spec.Clip as AnimationClip;
        // A clip fitted to a cadence is a big swing from the Stylized pack; the rest set their timing from their own hand's top speed.
        bool fitted = source != null || spec.LandsAt > 0f;
        float length = clip != null ? clip.length : 0.6f;
        float contact = clip == null ? 0.3f : fitted ? Mathf.Min(DrivenContact(clip, spec.Hand) + spec.ContactLag, length) : Contact(clip, spec.Hand);
        // Nothing about a leap is slowed: a slow build-up in the air is a player hanging there.
        bool leaps = fitted && !spec.Looped && Leaps(clip, out _);
        float hitDelay, recovery, followThrough;
        if (fitted)
        {
            hitDelay = source != null ? source.HitDelay : spec.LandsAt;
            hitDelay = spec.LandsBy > 0f ? Mathf.Min(hitDelay, spec.LandsBy) : hitDelay;
            recovery = source != null ? source.RecoveryTime : spec.RecoversAt;
            // What is left of the clip is played out in the time the swing has left.
            followThrough = Mathf.Clamp((length - contact) / Mathf.Max(0.05f, recovery - hitDelay), 0.7f, 1.8f);
        }
        else
        {
            // The wind-up plays at the attack's speed, the strike after the hit at its own (FollowThrough).
            hitDelay = contact / spec.Speed;
            recovery = spec.RecoverAfterContact >= 0f ? hitDelay + spec.RecoverAfterContact : hitDelay + (length - contact) / spec.FollowThrough;
            followThrough = spec.FollowThrough;
        }

        // A slow build-up and a fast strike, in the same time as before.
        float strikeClip = spec.StrikeClip >= 0f ? spec.StrikeClip : Mathf.Min(0.09f, contact * 0.4f);
        float strikeSpeed = spec.StrikeSpeed;
        float buildUp = StrikeRules.WindupSpeed(contact, hitDelay, strikeClip, strikeSpeed);
        // A swing too quick for a build-up (a dagger's stab) or one that loops plays at one speed.
        if (spec.Looped || leaps || buildUp >= strikeSpeed)
        {
            buildUp = strikeSpeed = spec.Looped ? 1f : contact / Mathf.Max(0.01f, hitDelay);
            strikeClip = 0f;
            followThrough = spec.Looped ? 1f : followThrough;
        }

        var data = new SerializedObject(attack);
        data.FindProperty("hitDelay").floatValue = hitDelay;
        data.FindProperty("recoveryTime").floatValue = Mathf.Max(recovery, hitDelay + 0.05f);
        data.FindProperty("animationState").stringValue = spec.State;
        data.FindProperty("animationSpeed").floatValue = buildUp;
        data.FindProperty("strikeSpeed").floatValue = strikeSpeed;
        data.FindProperty("strikeLead").floatValue = strikeClip / strikeSpeed;
        data.FindProperty("followThroughSpeed").floatValue = followThrough;
        if (spec.Impact != 1f && data.FindProperty("impact").floatValue == 1f)
        {
            data.FindProperty("impact").floatValue = spec.Impact;
        }

        // The slash arc runs the way the weapon hand moves at the contact.
        (SlashSweep sweep, float tilt) = SlashFor(clip != null ? HandVelocity(clip, contact, spec.Hand) : Vector3.zero);
        data.FindProperty("slashSweep").enumValueIndex = (int)sweep;
        data.FindProperty("slashTilt").floatValue = tilt;
        if (source == null && (created || data.FindProperty("poiseDamage").floatValue == 10f && spec.Poise != 10f))
        {
            data.FindProperty("poiseDamage").floatValue = spec.Poise;
        }

        if (created)
        {
            data.FindProperty("displayName").stringValue = System.IO.Path.GetFileNameWithoutExtension(spec.Asset);
        }

        if (created && source == null)
        {
            data.FindProperty("weaponDamageRatio").floatValue = spec.Ratio;
            data.FindProperty("range").floatValue = spec.Range;
            data.FindProperty("arc").floatValue = spec.Arc;
            data.FindProperty("maxTargets").intValue = spec.Targets;
            data.FindProperty("lunge").floatValue = spec.Lunge;
            data.FindProperty("slashColor").colorValue = spec.Slash;
        }
        else if (data.FindProperty("lunge").floatValue <= 0f && source == null)
        {
            data.FindProperty("lunge").floatValue = spec.Lunge;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    // A null let-through leaves how well the weapon guards as it is.
    private static void Weapon(string asset, string[] chain, string heavy, string counter, float? letThrough, Dictionary<string, AttackDefinition> made)
    {
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponFolder}/{asset}.asset");
        if (weapon == null)
        {
            Debug.LogWarning($"Combat animations: no weapon {asset}.");
            return;
        }

        var data = new SerializedObject(weapon);
        SerializedProperty combo = data.FindProperty("combo");
        combo.arraySize = chain.Length;
        for (int i = 0; i < chain.Length; i++)
        {
            combo.GetArrayElementAtIndex(i).objectReferenceValue = made[chain[i]];
        }

        data.FindProperty("basicAttack").objectReferenceValue = made[chain[0]];
        data.FindProperty("heavyAttack").objectReferenceValue = heavy != null ? made[heavy] : null;
        data.FindProperty("counterAttack").objectReferenceValue = counter != null ? made[counter] : null;
        if (letThrough.HasValue)
        {
            data.FindProperty("guardLetThrough").floatValue = letThrough.Value;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // The heavy roll's barge: a shove with no weapon behind it that knocks poise out of what it reaches.
    private static void AddShoulderBarge()
    {
        string path = ComboFolder + "/Shoulder Barge.asset";
        var barge = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        if (barge == null)
        {
            barge = ScriptableObject.CreateInstance<AttackDefinition>();
            AssetDatabase.CreateAsset(barge, path);
            var data = new SerializedObject(barge);
            data.FindProperty("displayName").stringValue = "Shoulder Barge";
            data.FindProperty("baseDamage").floatValue = 6f;
            data.FindProperty("weaponDamageRatio").floatValue = 0f;
            data.FindProperty("range").floatValue = 1.7f;
            data.FindProperty("arc").floatValue = 120f;
            data.FindProperty("maxTargets").intValue = 3;
            data.FindProperty("hitDelay").floatValue = 0f;
            data.FindProperty("recoveryTime").floatValue = 0.1f;
            data.FindProperty("poiseDamage").floatValue = 30f;
            data.FindProperty("canCrit").boolValue = false;
            data.FindProperty("slashColor").colorValue = Color.clear;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var dodge = new SerializedObject(root.GetComponent<PlayerDodge>());
            if (dodge.FindProperty("shoulderBarge").objectReferenceValue == null)
            {
                dodge.FindProperty("shoulderBarge").objectReferenceValue = barge;
                dodge.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Skills are paid with Focus, earned by fighting well; the stronger the skill, the more it takes. Only skills that
    // have no cost yet are set.
    private static void SetSkillFocusCosts()
    {
        var costs = new Dictionary<string, float>
        {
            ["WolfsLunge"] = 25f, ["WhirlingSteel"] = 40f, ["AshladsEmber"] = 35f, ["FrostRune"] = 35f,
            ["TrollsbaneWrath"] = 60f, ["HuldrasBlessing"] = 30f,
        };
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null || skill.FocusCost > 0f || !costs.TryGetValue(skill.name, out float cost))
            {
                continue;
            }

            var data = new SerializedObject(skill);
            data.FindProperty("focusCost").floatValue = cost;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<CombatTechniqueSettings>(SettingsPath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CombatTechniqueSettings>(), SettingsPath);
        }
    }

    private static void AddGuardToPlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            bool changed = false;
            if (root.GetComponent<PlayerGuard>() == null)
            {
                root.AddComponent<PlayerGuard>();
                changed = true;
            }

            if (root.GetComponent<PlayerFocus>() == null)
            {
                root.AddComponent<PlayerFocus>();
                changed = true;
            }

            if (root.GetComponent<PlayerEating>() == null)
            {
                root.AddComponent<PlayerEating>();
                changed = true;
            }

            if (root.GetComponent<PlayerFocusTarget>() == null)
            {
                root.AddComponent<PlayerFocusTarget>();
                changed = true;
            }

            if (root.GetComponent<WeaponTrail>() == null)
            {
                root.AddComponent<WeaponTrail>();
                changed = true;
            }

            if (root.GetComponent<TwoHandGrip>() == null)
            {
                root.AddComponent<TwoHandGrip>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Directory(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
