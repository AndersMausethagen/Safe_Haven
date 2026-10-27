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
