using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tries the bow in Play mode (`unity command run_script --file AgentScripts/ProbeArchery.cs --entry ProbeArchery.<Step>`):
/// Open opens the raid scene; Arm gives the player the bow and arrows and stands a monster in front of them; Shoot
/// draws for the given seconds and looses, reporting the hit; Report shows the archery state.
/// </summary>
public static class ProbeArchery
{
    public static string Open(string scene)
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        {
            return "playing or unsaved changes";
        }

        EditorSceneManager.OpenScene(scene);
        return "opened " + scene;
    }

    // Each run_script call is a fresh assembly, so the chosen monster is found again: the nearest ordinary one.
    private static MonsterAI chosen => MonsterAI.Active.Where(m => m != null && m.GetComponent<Damageable>().IsAlive && !m.name.Contains("Guardian") && !m.name.Contains("Troll"))
        .OrderBy(m => (m.transform.position - Object.FindAnyObjectByType<Player>().transform.position).sqrMagnitude).FirstOrDefault();

    public static string Arm(float distance)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var bow = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeWoodenBow.asset");
        var arrows = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeStoneArrow.asset");
        var items = player.GetComponent<ItemInventory>();
        var equipment = player.GetComponent<PlayerEquipment>();
        if (equipment.MainHand.IsEmpty || equipment.MainHand.Item != bow)
        {
            items.Give(bow, 1);
            int slot = Enumerable.Range(0, items.Container.SlotCount).First(i => !items.Container.GetSlot(i).IsEmpty && items.Container.GetSlot(i).Item == bow);
            equipment.EquipTo(items.Container, slot, GearSlot.MainHand);
        }

        player.GetComponent<Damageable>().AddInvulnerability(typeof(ProbeArchery));
        if (items.Container.Count(arrows) < 10)
        {
            items.Give(arrows, 20);
        }

        MonsterAI monster = chosen;
        string placed = "no monster";
        if (monster != null)
        {
            Vector3 ahead = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 spot = player.transform.position + ahead * distance;
            var agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool sampled = UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas);
            bool warped = sampled && agent.Warp(hit.position);
            if (!warped)
            {
                monster.transform.position = spot;
            }

            placed = $"sampled={sampled} warped={warped} ";

            placed += $"{monster.name} at {Vector3.Distance(monster.transform.position, player.transform.position):F1} m, health {monster.GetComponent<Damageable>().CurrentHealth:F0}";
        }

        return $"main hand={equipment.MainHand.Item?.DisplayName} arrows={items.Container.Count(arrows)} {placed}\n" + Report();
    }

    /// <summary>Points the (virtual) mouse at the nearest monster, offset by <paramref name="offset"/> metres sideways.</summary>
    public static async Task<string> PointAtMonster(float offset)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        if (chosen == null)
        {
            return "no monster";
        }

        Vector3 screen = Point(offset);
        await Task.Delay(300);
        return $"pointer at {screen:F0} ({chosen.name} {Vector3.Distance(chosen.transform.position, player.transform.position):F1} m) " + Report();
    }

    private static Vector3 Point(float offset)
    {

        Vector3 screen = Camera.main.WorldToScreenPoint(chosen.transform.position + Camera.main.transform.right * offset);
        UnityEngine.InputSystem.LowLevel.InputState.Change(UnityEngine.InputSystem.Mouse.current.position, (Vector2)screen);
        return screen;
    }

    public static async Task<string> Shoot(float drawSeconds)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var archery = player.GetComponent<PlayerArchery>();
        var machine = player.GetComponent<PlayerStateMachine>();
        Damageable target = archery.Target;
        float before = target != null ? target.CurrentHealth : -1f;
        var arrows = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeStoneArrow.asset");
        int arrowsBefore = player.GetComponent<ItemInventory>().Container.Count(arrows);
        machine.RequestAttack();
        string mid = "";
        for (float waited = 0f; waited < drawSeconds; waited += 0.1f)
        {
            if (chosen != null)
            {
                Point(0f);
            }

            await Task.Delay(100);
            if (mid.Length == 0 && waited >= drawSeconds * 0.5f)
            {
                mid = $"mid-draw: state={archery.State} draw={archery.Draw:F2} target={archery.Target?.name} {Animation(player)}";
            }
        }

        target = archery.Target;
        before = target != null ? target.CurrentHealth : -1f;
        float draw = archery.Draw;
        machine.ReleaseAttack();
        string after = $"after release: state={archery.State} {Animation(player)}";
        await Task.Delay(1500);
        float now = target != null ? target.CurrentHealth : -1f;
        int arrowsAfter = player.GetComponent<ItemInventory>().Container.Count(arrows);
        return $"{mid}\ndraw at release={draw:F2}; {after}\ntarget={target?.name} health {before:F0} -> {now:F0}; arrows {arrowsBefore} -> {arrowsAfter}\n" + Report();
    }

    /// <summary>Plays <paramref name="scene"/> without opening it (the open scene and its unsaved changes stay as they are).</summary>
    public static string PlayIn(string scene)
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
        EditorApplication.EnterPlaymode();
        return "entering play in " + scene;
    }

    public static string StopAndForget()
    {
        EditorApplication.ExitPlaymode();
        EditorSceneManager.playModeStartScene = null;
        return "stopped";
    }

    /// <summary>Under the action camera: stands the nearest ordinary monster where the crosshair meets the ground,
    /// <paramref name="distance"/> m out, so the bow targets it.</summary>
    public static async Task<string> PlaceInCrosshair(float distance)
    {
        MonsterAI monster = chosen;
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Player player = Object.FindAnyObjectByType<Player>();
        Vector3 ahead = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
        Vector3 spot = player.transform.position + ahead * distance;
        if (monster == null || !UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return "no monster or no ground there";
        }

        monster.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(hit.position);
        await Task.Delay(300);
        return $"{monster.name} at {Vector3.Distance(monster.transform.position, player.transform.position):F1} m; " + Report();
    }

    /// <summary>Launches a weak fire arrow from 2 m straight at the nearest ordinary monster, then watches it burn.</summary>
    public static async Task<string> FireAt()
    {
        MonsterAI monster = chosen;
        Player player = Object.FindAnyObjectByType<Player>();
        var fire = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/FireArrow.asset");
        var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/CrudeWoodenBow.asset");
        if (monster == null)
        {
            return "no monster";
        }

        var body = monster.GetComponent<Damageable>();
        Vector3 centre = monster.GetComponentInChildren<Collider>().bounds.center;
        Vector3 from = centre + Vector3.ProjectOnPlane(player.transform.position - centre, Vector3.up).normalized * 2f;
        string events = "";
        void Seen(Damageable victim, DamageInfo damage, DamageResult result)
        {
            if (victim == body)
            {
                events += $" [{damage.Amount:F1}->{result.Damage:F1} {damage.Type} {damage.Technique} crit={damage.IsCritical} by {(damage.Source != null ? damage.Source.name : "-")} attack={(damage.Attack != null ? damage.Attack.name : "-")}]";
            }
        }

        Damageable.AnyDamaged += Seen;
        ArrowProjectile arrow = PrefabPool.Spawn(fire.Arrow.Projectile, from, Quaternion.LookRotation(centre - from));
        arrow.Launch(player.GetComponent<Combatant>(), bow.ShotAttack, fire, 1f, (centre - from).normalized * 30f);
        float before = body.CurrentHealth;
        await Task.Delay(300);
        bool burning = body.TryGetComponent(out Burning flames) && flames.IsBurning;
        float hit = body.CurrentHealth;
        string name = monster.name;
        string log = "";
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(500);
            log += body != null ? $" {body.CurrentHealth:F0}{(body.IsAlive ? "" : " dead")}" : " gone";
        }

        Damageable.AnyDamaged -= Seen;
        return $"{name}: health {before:F0} -> {hit:F0} on the hit, burning={burning}, then every 0.5 s:{log}; damage:{events} multiplier={(body != null ? body.DamageTakenMultiplier : 0f)}";
    }

    /// <summary>Gives <paramref name="amount"/> of an item from Data/Items.</summary>
    public static string Give(string file, int amount)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/" + file + ".asset");
        int given = Object.FindAnyObjectByType<Player>().GetComponent<ItemInventory>().Give(item, amount);
        return $"gave {given} {item.DisplayName}";
    }

    public static string Cycle()
    {
        var archery = Object.FindAnyObjectByType<Player>().GetComponent<PlayerArchery>();
        archery.CycleArrow();
        return "next arrow " + archery.NextArrow?.DisplayName;
    }

    public static string Aim(bool raised)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        player.GetComponent<PlayerStateMachine>().RequestGuard(raised);
        return $"aiming={player.GetComponent<PlayerArchery>().IsAiming} guarding={player.GetComponent<PlayerGuard>().IsGuarding}";
    }

    /// <summary>Readies a counter as a perfect dodge would, then presses attack once: the instant shot.</summary>
    public static async Task<string> CounterShot()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var archery = player.GetComponent<PlayerArchery>();
        Point(0f);
        await Task.Delay(200);
        Damageable target = archery.Target;
        float before = target != null ? target.CurrentHealth : -1f;
        player.GetComponent<PlayerCombat>().GrantCounter();
        bool ready = archery.CounterShotReady;
        var machine = player.GetComponent<PlayerStateMachine>();
        string hits = "";
        void Seen(Damageable victim, DamageInfo damage, DamageResult result) => hits += $" [{victim.name} {result.Damage:F1} {damage.Technique}]";
        string shot = "";
        void Shot(ArrowShot s) => shot = $"draw={s.Draw:F1} counter={s.Counter} v={s.Velocity.magnitude:F0} from {s.Origin:F1} dir {s.Velocity.normalized:F2} aim {archery.AimPoint:F1}";
        Damageable.AnyDamaged += Seen;
        archery.Shot += Shot;
        machine.RequestAttack();
        string state = archery.State.ToString();
        machine.ReleaseAttack();
        await Task.Delay(1500);
        Damageable.AnyDamaged -= Seen;
        archery.Shot -= Shot;
        return $"ready={ready} after press state={state} counterLeft={archery.CounterShotReady} target={target?.name} health {before:F0} -> {(target != null ? target.CurrentHealth : -1f):F0}; shot {shot}; hits{hits}";
    }

    /// <summary>Draws to the perfect window (full draw plus a hair) and lets go; reports whether the hit was critical.</summary>
    public static async Task<string> PerfectShot()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var archery = player.GetComponent<PlayerArchery>();
        Point(0f);
        await Task.Delay(200);
        bool critical = false;
        float dealt = 0f;
        void Seen(Damageable victim, DamageInfo damage, DamageResult result)
        {
            if (damage.Source != null && damage.Source.gameObject == player.gameObject)
            {
                critical |= damage.IsCritical;
                dealt += result.Damage;
            }
        }

        Damageable.AnyDamaged += Seen;
        try
        {
            var machine = player.GetComponent<PlayerStateMachine>();
            machine.RequestAttack();
            while (archery.IsDrawing && !archery.InPerfectWindow)
            {
                Point(0f);
                await Task.Yield();
            }

            bool window = archery.InPerfectWindow;
            Damageable aimed = archery.Target;
            machine.ReleaseAttack();
            await Task.Delay(1200);
            bool burning = aimed != null && aimed.TryGetComponent(out Burning fire) && fire.IsBurning;
            return $"released in window={window} at {aimed?.name} critical={critical} dealt={dealt:F1} burning={burning} arrow={archery.NextArrow?.DisplayName}";
        }
        finally
        {
            Damageable.AnyDamaged -= Seen;
        }
    }

    /// <summary>Burn check: the nearest monster's health now and after <paramref name="seconds"/>.</summary>
    public static async Task<string> Watch(float seconds)
    {
        Damageable body = chosen != null ? chosen.GetComponent<Damageable>() : null;
        if (body == null)
        {
            return "no monster";
        }

        float before = body.CurrentHealth;
        bool burning = body.TryGetComponent(out Burning fire) && fire.IsBurning;
        await Task.Delay(Mathf.RoundToInt(seconds * 1000f));
        return $"{body.name} burning={burning} health {before:F0} -> {body.CurrentHealth:F0}";
    }

    /// <summary>Shoots at the ground <paramref name="distance"/> m ahead until an arrow sticks recoverably, then walks the
    /// player onto it.</summary>
    public static async Task<string> Recover(float distance)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var bag = player.GetComponent<ItemInventory>();
        var arrows = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeStoneArrow.asset");
        for (int i = 0; i < 8; i++)
        {
            Vector3 ground = player.transform.position + player.transform.forward * distance;
            Vector3 screen = Camera.main.WorldToScreenPoint(ground);
            UnityEngine.InputSystem.LowLevel.InputState.Change(UnityEngine.InputSystem.Mouse.current.position, (Vector2)screen);
            var machine = player.GetComponent<PlayerStateMachine>();
            machine.RequestAttack();
            await Task.Delay(900);
            machine.ReleaseAttack();
            await Task.Delay(1000);
            ArrowProjectile stuck = Object.FindObjectsByType<ArrowProjectile>().FirstOrDefault(a => a.isActiveAndEnabled && a.Recoverable);
            if (stuck != null)
            {
                int before = bag.Container.Count(arrows);
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = stuck.transform.position;
                player.GetComponent<CharacterController>().enabled = true;
                await Task.Delay(400);
                return $"shots {i + 1}; picked up: arrows {before} -> {bag.Container.Count(arrows)}, arrow still out={stuck.isActiveAndEnabled && stuck.Recoverable}";
            }
        }

        return "no recoverable arrow in 8 shots";
    }

    public static string Press()
    {
        Object.FindAnyObjectByType<Player>().GetComponent<PlayerStateMachine>().RequestAttack();
        return "pressed";
    }

    public static string Release()
    {
        Object.FindAnyObjectByType<Player>().GetComponent<PlayerStateMachine>().ReleaseAttack();
        return "released " + Report();
    }

    /// <summary>Renders the player from <paramref name="yaw"/> degrees around (0 = in front) into a PNG.</summary>
    public static string CloseUp(string path, float yaw)
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var go = new GameObject("Probe Camera");
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 35f;
            Vector3 centre = player.transform.position + Vector3.up * 1.2f;
            Vector3 from = Quaternion.Euler(0f, yaw, 0f) * player.transform.forward;
            go.transform.position = centre + from * 3.2f + Vector3.up * 0.4f;
            go.transform.LookAt(centre);
            var texture = new RenderTexture(768, 768, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(image);
            return "saved " + path;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>Where the bow arm points and where the bow's tips are, relative to the player.</summary>
    public static string Pose()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        Animator animator = player.GetComponentInChildren<Animator>();
        Transform root = player.transform;
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
        Vector3 arm = hand.position - shoulder.position;
        string text = $"arm local={root.InverseTransformDirection(arm.normalized):F2} yaw={Vector3.SignedAngle(root.forward, Vector3.ProjectOnPlane(arm, Vector3.up), Vector3.up):F0} " +
                      $"hipsFwd yaw={Vector3.SignedAngle(root.forward, Vector3.ProjectOnPlane(hips.forward, Vector3.up), Vector3.up):F0} " +
                      $"chestFwd yaw={Vector3.SignedAngle(root.forward, Vector3.ProjectOnPlane(chest.forward, Vector3.up), Vector3.up):F0} " +
                      $"hand local={root.InverseTransformPoint(hand.position):F2} rhand local={root.InverseTransformPoint(rightHand.position):F2}";
        GameObject bow = player.GetComponent<HeldItemView>().ShownModel;
        if (bow != null)
        {
            foreach (string bone in new[] { "B-bowTip01", "B-bowTip02", "B-nockPoint", "B-root" })
            {
                Transform t = bow.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == bone);
                if (t != null)
                {
                    text += $" | {bone} local={root.InverseTransformPoint(t.position):F2}";
                }
            }

            Renderer r = bow.GetComponentInChildren<Renderer>();
            text += $" | bow bounds size={r.bounds.size:F2} centre local={root.InverseTransformPoint(r.bounds.center):F2} lossy={bow.transform.lossyScale:F2}";
        }

        return text;
    }

    public static string Report()
    {
        Player player = Object.FindAnyObjectByType<Player>();
        var archery = player.GetComponent<PlayerArchery>();
        var outline = player.GetComponent<ArcheryTargetOutline>();
        var held = player.GetComponent<HeldItemView>();
        return $"bow={archery.Bow?.DisplayName} state={archery.State} target={archery.Target?.name} outlined={outline.Shown?.name} " +
               $"aim={archery.AimPoint:F1} origin={archery.Origin:F1} pointer={(CameraPointer.UsesScreenCenter ? "crosshair" : "cursor")} " +
               $"held={held.Shown?.name} in {(held.ShownModel != null ? held.ShownModel.transform.parent.name : "-")} {Animation(player)}";
    }

    private static string Animation(Player player)
    {
        Animator animator = player.GetComponentInChildren<Animator>();
        int layer = animator.GetLayerIndex(PlayerArcheryAnimation.LayerName);
        if (layer < 0)
        {
            return "no bow layer";
        }

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layer);
        return $"bowLayer w={animator.GetLayerWeight(layer):F2} clip={(clips.Length > 0 ? clips[0].clip.name : "-")}";
    }
}
