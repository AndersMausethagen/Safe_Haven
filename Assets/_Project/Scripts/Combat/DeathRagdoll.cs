using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a killed monster leaves: its body, lifted out of the monster (which still goes through its own death: loot, quests, removal)
/// and given over to physics, thrown the way the killing blow went and as hard as it struck. A skinned humanoid gets jointed limbs
/// on its bones; a monster built from rigid parts gets each part jointed to its body. What bleeds goes on pumping from where it was struck,
/// in pulses that weaken and fling blood around it, and once it lies still a pool spreads out under it. The corpse rests, sinks into the
/// ground and is gone after a while (Combat Feel › Corpses); the pool stays a little longer. Its colliders are on Ignore Raycast and ignore
/// the player, so a corpse never blocks the player, the aim or a swing. Content Editor › Monsters › Ragdoll On Death turns it off for what
/// has no body (a Marsh Light).
/// <para>
/// Gore (2026-11-14, Combat Feel › Gore; never while the Blood option is off): a two-handed blunt weapon's killing blow
/// can burst a small creature (Can Be Smashed) into pieces of its own body, gobbets and a wide splatter; a slashing killing
/// blow can take the head or a limb (Can Lose Limbs), which flies off while the stump pumps blood harder and longer.
/// </para>
/// </summary>
public sealed class DeathRagdoll : MonoBehaviour
{
    private const int IgnoreRaycastLayer = 2;
    private const float SinkSpeed = 0.35f;
    private const float SinkSeconds = 2.5f;
    // Kilograms for a body of person size. Masses only matter relative to each other and to the velocity it is given.
    private const float BodyMass = 60f;
    // A body counts as lying still under this speed for this long.
    private const float StillSpeed = 0.45f;
    private const float StillSeconds = 0.2f;
    // How wide a pool ends up, as a share of how wide the body lies, in metres at the least and the most.
    private const float PoolShareOfBody = 1.7f;
    private const float PoolSmallest = 1.6f;
    private const float PoolLargest = 7f;

    private static readonly List<DeathRagdoll> Corpses = new List<DeathRagdoll>();

    private readonly List<Rigidbody> bodies = new List<Rigidbody>();
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private static readonly int DeathState = Animator.StringToHash("Death");
    private float settleAt;
    private float sinkAt;
    private bool settled;

    // The bleeding: where from, what colour, how long it has left and whether its pool has begun.
    private bool bleeds;
    private Color blood;
    private Rigidbody wound;
    private Rigidbody trunk;
    private Vector3 blowDirection;
    private float bleedStart;
    private float nextSpurt;
    private float stillFor;
    private bool pooled;
    // A cut: where the stump is (it pumps from there), the piece that came off, and how much harder it bleeds.
    private Transform stump;
    private Rigidbody severed;
    private float bleedScale = 1f;
    private int spurts;
    private static Material goreMaterial;

    /// <summary>Turns <paramref name="victim"/>'s body into a corpse, if it is a monster that leaves one.</summary>
    public static DeathRagdoll Play(Damageable victim, in DamageInfo blow, float force)
    {
        if (victim == null || !victim.DestroysOnDeath || !victim.TryGetComponent(out MonsterAI monster)
            || monster.Definition == null || !monster.Definition.RagdollOnDeath)
        {
            return null;
        }

        Animator animator = victim.GetComponentInChildren<Animator>();
        bool humanoid = animator != null && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Hips) != null;
        var corpse = new GameObject($"{victim.name} (corpse)").AddComponent<DeathRagdoll>();
        corpse.transform.SetPositionAndRotation(victim.transform.position, victim.transform.rotation);

        bool byAnimation = monster.Definition.FallsByAnimation && animator != null && animator.HasState(0, DeathState);
        if (animator != null && !byAnimation)
        {
            animator.enabled = false;
        }

        foreach (Collider solid in victim.GetComponents<Collider>())
        {
            solid.enabled = false;
        }

        corpse.TakeBody(victim.transform);
        CreatureLook.CloseEyes(corpse.gameObject);
        MonsterDefinition definition = monster.Definition;
        CombatFeelSettings feel = CombatFeelSettings.Current;
        bool gore = GameOptions.Blood.On && !byAnimation;
        if (gore && definition.CanBeSmashed && HeavyBlunt(blow) && Random.value < feel.SmashChance)
        {
            corpse.Smash(blow, force, definition);
            corpse.Register();
            return corpse;
        }

        bool sever = gore && definition.CanLoseLimbs && blow.Strike == StrikeKind.Slash && Random.value < feel.SeverChance;
        if (byAnimation)
        {
            // Nothing goes loose: it keeps its Animator, plays its fall and is left where it lands.
            animator.applyRootMotion = false;
            animator.CrossFadeInFixedTime(DeathState, 0.1f, 0);
        }
        else if (humanoid)
        {
            corpse.BuildHumanoid(animator);
        }
        else
        {
            corpse.BuildFromParts(sever ? CutPoint(corpse.transform) : null);
        }

        corpse.Throw(blow, force);
        corpse.StartBleeding(monster.Definition, blow);
        if (sever)
        {
            if (humanoid)
            {
                corpse.SeverLimb(animator, blow, feel);
            }
            else
            {
                corpse.SeverPart(blow, feel);
            }
        }

        corpse.Register();
        return corpse;
    }

    // ----- Gore -----

    // A killing blow from a two-handed blunt weapon in the player's hands.
    private static bool HeavyBlunt(in DamageInfo blow)
    {
        if (blow.Strike != StrikeKind.Blunt || blow.Source == null || !blow.Source.TryGetComponent(out PlayerEquipment gear))
        {
            return false;
        }

        ItemStack held = gear.MainHand;
        return !held.IsEmpty && held.Item.Weapon != null && held.Item.Weapon.Stance == WeaponStance.TwoHanded;
    }

    // A skinned creature that isn't a humanoid (a wolf, a boar): its head bone, cut out of the skin before it is baked.
    private static Transform CutPoint(Transform body)
    {
        Transform neck = null;
        foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            foreach (Transform bone in skin.bones)
            {
                if (bone == null)
                {
                    continue;
                }

                string name = bone.name.ToLowerInvariant();
                if (name.Contains("head") && !name.Contains("end") && !name.Contains("top"))
                {
                    return bone;
                }

                neck = neck == null && name.Contains("neck") ? bone : neck;
            }
        }

        return neck;
    }

    // The whole body in pieces: chunks of its own meshes flung out from the blow, gobbets of gore, and blood all round.
    private void Smash(in DamageInfo blow, float force, MonsterDefinition definition)
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        BakeSkins();
        var parts = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
        parts.RemoveAll(part => OutlineCopies.IsCopy(part) || part.GetComponentInParent<ArrowProjectile>() != null || !part.enabled);
        if (parts.Count == 0)
        {
            Throw(blow, force);
            return;
        }

        Bounds body = parts[0].bounds;
        foreach (MeshRenderer part in parts)
        {
            body.Encapsulate(part.bounds);
        }

        var readable = parts.FindAll(part => part.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh.isReadable);
        List<BodyCutter.Chunk> chunks = BodyCutter.Break(readable, new Vector3Int(2, 3, 2));
        foreach (Renderer shown in GetComponentsInChildren<Renderer>())
        {
            shown.enabled = false;
        }

        Vector3 blowWay = blow.Direction.sqrMagnitude > 1e-4f ? blow.Direction.normalized : -transform.forward;
        foreach (BodyCutter.Chunk chunk in chunks)
        {
            var piece = new GameObject("Piece", typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(transform, true);
            piece.transform.position = chunk.Centre;
            piece.GetComponent<MeshFilter>().sharedMesh = chunk.Mesh;
            piece.GetComponent<MeshRenderer>().sharedMaterials = chunk.Materials;
            bakedMeshes.Add(chunk.Mesh);
            Fling(piece, chunk.Mesh.bounds, body.center, blowWay, feel, 4f);
        }

        // What can't be cut (a mesh not kept readable) flies off whole.
        foreach (MeshRenderer part in parts)
        {
            if (!readable.Contains(part))
            {
                part.enabled = true;
                part.transform.SetParent(transform, true);
                Fling(part.gameObject, part.GetComponent<MeshFilter>().sharedMesh.bounds, body.center, blowWay, feel, 6f);
            }
        }

        Color red = definition.BloodColor;
        if (definition.Bleeds)
        {
            for (int i = 0; i < feel.SmashGobbets; i++)
            {
                var gobbet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gobbet.name = "Gore";
                gobbet.transform.SetParent(transform, true);
                gobbet.transform.position = body.center + Random.insideUnitSphere * 0.2f;
                gobbet.transform.localScale = Vector3.Scale(Vector3.one * Random.Range(0.07f, 0.16f), new Vector3(Random.Range(0.6f, 1.4f), Random.Range(0.5f, 1f), Random.Range(0.6f, 1.4f)));
                gobbet.GetComponent<MeshRenderer>().sharedMaterial = Gore(red);
                Fling(gobbet, new Bounds(Vector3.zero, Vector3.one), body.center, blowWay, feel, 0.4f, 1.3f);
            }

            for (int i = 0; i < 7; i++)
            {
                Vector3 way = (Random.onUnitSphere + Vector3.up * 0.6f + blowWay * 0.5f).normalized;
                BloodEffects.Spray(body.center, way, 1f, red);
            }

            BloodEffects.PoolUnder(body.center, Mathf.Clamp(Mathf.Max(body.size.x, body.size.z) * 2.2f, 2f, 5f), red);
            for (int i = 0; i < 8; i++)
            {
                Vector2 off = Random.insideUnitCircle * feel.SmashSplatterReach;
                BloodEffects.Pool(body.center + new Vector3(off.x, 0.5f, off.y), Random.Range(0.4f, 1.3f), red);
            }
        }

        trunk = bodies.Count > 0 ? bodies[0] : null;
        wound = null;
        bleeds = false;
        pooled = true;
        settleAt = Time.time + feel.CorpseSettleSeconds;
        sinkAt = Time.time + feel.CorpseSeconds;
    }

    // Out from the middle of the body and along the blow, spinning.
    private void Fling(GameObject piece, Bounds local, Vector3 middle, Vector3 blowWay, CombatFeelSettings feel, float mass, float speedScale = 1f)
    {
        Rigidbody body = AddBody(piece.transform, mass);
        if (!piece.TryGetComponent(out Collider shape))
        {
            var box = piece.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = Vector3.Max(local.size, Vector3.one * 0.05f);
            shape = box;
        }

        Prepare(shape);
        Vector3 out_ = piece.transform.position - middle;
        Vector3 way = (out_.sqrMagnitude > 1e-4f ? out_.normalized : Random.onUnitSphere) + blowWay * 0.7f + Vector3.up * 0.8f;
        body.linearVelocity = way.normalized * Random.Range(feel.SmashSpeedMin, feel.SmashSpeedMax) * speedScale;
        body.angularVelocity = Random.insideUnitSphere * 14f;
    }

    private static Material Gore(Color blood)
    {
        if (goreMaterial == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            goreMaterial = new Material(lit != null ? lit : Shader.Find("Standard")) { name = "Gore (runtime)" };
            goreMaterial.SetFloat("_Smoothness", 0.72f);
        }

        Color flesh = Color.Lerp(blood, new Color(0.45f, 0.08f, 0.1f, 1f), 0.35f);
        flesh.a = 1f;
        goreMaterial.color = flesh;
        return goreMaterial;
    }

    // A humanoid loses its head or a limb: the part is cut out of each skin into a piece of its own that flies off, the bone
    // folds to nothing on the body (its skin closes to a stump at the joint), and the stump pumps blood.
    private void SeverLimb(Animator animator, in DamageInfo blow, CombatFeelSettings feel)
    {
        Transform limb = animator.GetBoneTransform(HumanBodyBones.Head);
        if (limb == null || Random.value > feel.SeverHeadShare)
        {
            float nearest = float.MaxValue;
            foreach (HumanBodyBones candidate in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm,
                         HumanBodyBones.RightLowerArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
            {
                Transform bone = animator.GetBoneTransform(candidate);
                float distance = bone != null ? (bone.position - blow.HitPoint).sqrMagnitude * Random.Range(0.7f, 1.3f) : float.MaxValue;
                if (distance < nearest)
                {
                    nearest = distance;
                    limb = bone;
                }
            }
        }

        if (limb == null)
        {
            return;
        }

        var root = new GameObject($"{limb.name} (cut off)");
        root.transform.SetParent(transform, true);
        root.transform.SetPositionAndRotation(limb.position, limb.rotation);
        bool any = false;
        var shape = new Bounds();
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (OutlineCopies.IsCopy(skin) || !skin.enabled || !BodyCutter.Split(skin, limb, out Mesh piece, out Mesh rest))
            {
                continue;
            }

            Object.Destroy(rest);
            bakedMeshes.Add(piece);
            var part = new GameObject(skin.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root.transform, true);
            part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            // The baked mesh is in the skin's parent's units (BodyCutter.Split).
            part.transform.localScale = skin.transform.parent != null ? skin.transform.parent.lossyScale : Vector3.one;
            part.GetComponent<MeshFilter>().sharedMesh = piece;
            part.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            Bounds inRoot = TransformBounds(piece.bounds, part.transform, root.transform);
            shape = any ? Encapsulated(shape, inRoot) : inRoot;
            any = true;
        }

        if (!any)
        {
            Destroy(root);
            return;
        }

        // The limb folds away on the body: its skin closes to the joint, and nothing of it collides any more.
        foreach (Collider part in limb.GetComponentsInChildren<Collider>())
        {
            Destroy(part);
        }

        foreach (Rigidbody part in limb.GetComponentsInChildren<Rigidbody>())
        {
            part.isKinematic = true;
        }

        limb.localScale = Vector3.one * 0.001f;
        // What rode beside it goes too (a jaw, ears): BodyCutter cut their skin off with it.
        if (limb.parent != null)
        {
            foreach (Transform sibling in limb.parent)
            {
                if (BodyCutter.RidesWith(sibling, limb))
                {
                    sibling.localScale = Vector3.one * 0.001f;
                }
            }
        }

        Severed(root, shape, limb, blow, feel);
    }

    // A creature of rigid parts (or a skinned animal whose head was cut out of its skin before it was baked): the part nearest
    // the blow, or the cut-out head, comes loose from the body.
    private void SeverPart(in DamageInfo blow, CombatFeelSettings feel)
    {
        CharacterJoint chosen = null;
        float nearest = float.MaxValue;
        foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>())
        {
            bool cut = joint.name.EndsWith("(cut off)");
            float distance = cut ? -1f : (joint.transform.position - blow.HitPoint).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                chosen = joint;
            }
        }

        if (chosen == null || chosen.connectedBody == null)
        {
            return;
        }

        Rigidbody body = chosen.connectedBody;
        var point = new GameObject("Stump").transform;
        point.SetParent(body.transform, true);
        point.position = body.ClosestPointOnBounds(chosen.transform.position);
        Destroy(chosen);
        var piece = chosen.GetComponent<Rigidbody>();
        stump = point;
        severed = piece;
        bleedScale = feel.SeverBleed;
        FreeOfBody(piece);
        Push(piece, blow);
    }

    private void Severed(GameObject root, Bounds shape, Transform limb, in DamageInfo blow, CombatFeelSettings feel)
    {
        Rigidbody piece = AddBody(root.transform, BodyMass * 0.06f);
        var box = root.AddComponent<BoxCollider>();
        box.center = shape.center;
        box.size = Vector3.Max(shape.size, Vector3.one * 0.06f);
        Prepare(box);
        stump = limb;
        severed = piece;
        bleedScale = feel.SeverBleed;
        FreeOfBody(piece);
        Push(piece, blow);
    }

    // It flies free of the body it came from: overlapping that body's colliders, it was held where it was cut.
    private void FreeOfBody(Rigidbody piece)
    {
        Collider[] own = piece.GetComponentsInChildren<Collider>();
        foreach (Collider part in GetComponentsInChildren<Collider>())
        {
            foreach (Collider mine in own)
            {
                if (part != mine && !part.transform.IsChildOf(piece.transform))
                {
                    Physics.IgnoreCollision(part, mine);
                }
            }
        }
    }

    private static void Push(Rigidbody piece, in DamageInfo blow)
    {
        Vector3 way = (blow.Direction.sqrMagnitude > 1e-4f ? blow.Direction.normalized : Random.onUnitSphere) * 0.7f + Vector3.up * 0.9f;
        piece.isKinematic = false;
        piece.linearVelocity = way.normalized * Random.Range(3.5f, 6.5f);
        piece.angularVelocity = Random.insideUnitSphere * 10f;
    }

    private static Bounds TransformBounds(Bounds local, Transform from, Transform to)
    {
        var result = new Bounds(to.InverseTransformPoint(from.TransformPoint(local.center)), Vector3.zero);
        Vector3 e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            result.Encapsulate(to.InverseTransformPoint(from.TransformPoint(local.center + corner)));
        }

        return result;
    }

    private static Bounds Encapsulated(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }

    private void StartBleeding(MonsterDefinition definition, in DamageInfo blow)
    {
        bleeds = definition.Bleeds;
        blood = definition.BloodColor;
        blowDirection = blow.Direction;
        bleedStart = Time.time;
        nextSpurt = Time.time + 0.04f;
        trunk = bodies.Count > 0 ? bodies[0] : null;
        foreach (Rigidbody body in bodies)
        {
            if (trunk == null || body.mass > trunk.mass)
            {
                trunk = body;
            }
        }
    }

    private void UpdateBleeding()
    {
        if (!bleeds)
        {
            return;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        float seconds = feel.CorpseBleedSeconds * bleedScale;
        float through = seconds > 0f ? (Time.time - bleedStart) / seconds : 1f;
        if (through < 1f && Time.time >= nextSpurt && (wound != null || stump != null))
        {
            nextSpurt = Time.time + Mathf.Lerp(feel.CorpseSpurtFirst, feel.CorpseSpurtLast, through) / bleedScale * Random.Range(0.8f, 1.25f);
            float strength = Mathf.Lerp(1f, 0.2f, through);
            if (stump != null)
            {
                // From the stump, along the limb that was there.
                Vector3 along = stump.parent != null ? (stump.position - stump.parent.position) : Vector3.up;
                Vector3 way = (along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.up) + Random.insideUnitSphere * 0.35f + Vector3.up * 0.3f;
                BloodEffects.Spurt(stump.position, way.normalized, Mathf.Min(1f, strength * 1.3f), blood);
                if (severed != null && spurts++ % 3 == 0)
                {
                    BloodEffects.Spurt(severed.worldCenterOfMass, Random.onUnitSphere + Vector3.up * 0.5f, strength * 0.5f, blood);
                }
            }
            else
            {
                BloodEffects.Spurt(wound.worldCenterOfMass, SpurtDirection(through), strength, blood);
            }
        }

        if (pooled)
        {
            return;
        }

        stillFor = trunk != null && !trunk.isKinematic && trunk.linearVelocity.sqrMagnitude < StillSpeed * StillSpeed ? stillFor + Time.deltaTime : 0f;
        if (stillFor >= StillSeconds || Time.time >= settleAt - 0.1f)
        {
            pooled = true;
            BeginPool();
        }
    }

    // At first it goes the way the blow sent it; after that anywhere around the body, low, so it runs along the ground.
    private Vector3 SpurtDirection(float through)
    {
        Vector2 around = Random.insideUnitCircle.normalized;
        Vector3 way = new Vector3(around.x, Random.Range(0.3f, 0.75f), around.y);
        return Vector3.Lerp(blowDirection + Vector3.up * 0.4f, way, Mathf.Clamp01(through * 4f)).normalized;
    }

    // Under the body, as wide as it lies, and a smaller one under the wound itself.
    private void BeginPool()
    {
        bool found = false;
        var lying = new Bounds();
        foreach (Renderer part in GetComponentsInChildren<Renderer>())
        {
            if (OutlineCopies.IsCopy(part) || part is ParticleSystemRenderer)
            {
                continue;
            }

            if (found)
            {
                lying.Encapsulate(part.bounds);
            }
            else
            {
                lying = part.bounds;
                found = true;
            }
        }

        float across = found ? Mathf.Max(lying.size.x, lying.size.z) : 1.5f;
        float size = Mathf.Clamp(across * PoolShareOfBody, PoolSmallest, PoolLargest);
        BloodEffects.PoolUnder(trunk != null ? trunk.worldCenterOfMass : transform.position, size, blood);
        if (stump != null)
        {
            BloodEffects.PoolUnder(stump.position, size * 0.8f, blood);
        }
        if (wound != null && wound != trunk)
        {
            BloodEffects.PoolUnder(wound.worldCenterOfMass, size * 0.55f, blood);
        }
    }

    // The Model child, or for a monster without one everything but its UI.
    private void TakeBody(Transform monster)
    {
        Transform model = monster.Find("Model");
        if (model != null)
        {
            model.SetParent(transform, true);
            return;
        }

        var children = new List<Transform>();
        foreach (Transform child in monster)
        {
            if (child.GetComponentInChildren<Canvas>(true) == null && child.GetComponentInChildren<AttackTelegraph>(true) == null)
            {
                children.Add(child);
            }
        }

        foreach (Transform child in children)
        {
            child.SetParent(transform, true);
        }
    }

    // ----- A skinned humanoid -----

    private void BuildHumanoid(Animator animator)
    {
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform torso = chest != null ? chest : spine;
        float height = head != null ? Vector3.Distance(hips.position, head.position) * 2.2f : 1.8f;
        float scale = height / 1.8f;
        float mass = BodyMass * scale;

        Rigidbody pelvis = Part(hips, null, 0.18f, mass, torso != null ? torso.position : hips.position + Vector3.up * 0.2f, 0.16f * scale);
        Rigidbody upper = torso != null ? Part(torso, pelvis, 0.2f, mass, head != null ? head.position : torso.position + Vector3.up * 0.3f, 0.17f * scale, 25f, 15f) : pelvis;
        if (head != null)
        {
            AddHead(head, upper, 0.08f, mass, scale);
        }

        Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, upper, mass, scale, 0.045f, 60f, 110f);
        Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, upper, mass, scale, 0.045f, 60f, 110f);
        Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, pelvis, mass, scale, 0.07f, 40f, 90f);
        Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, pelvis, mass, scale, 0.07f, 40f, 90f);

        // Bones move under what the skin's bounds were made for.
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.updateWhenOffscreen = true;
        }
    }

    private void Limb(Animator animator, HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones endBone, Rigidbody parent, float mass, float scale,
        float radius, float swing, float bend)
    {
        Transform upper = animator.GetBoneTransform(upperBone);
        Transform lower = animator.GetBoneTransform(lowerBone);
        Transform end = animator.GetBoneTransform(endBone);
        if (upper == null || lower == null)
        {
            return;
        }

        Rigidbody top = Part(upper, parent, 0.075f, mass, lower.position, radius * scale, swing, swing);
        Part(lower, top, 0.05f, mass, end != null ? end.position : lower.position - Vector3.up * 0.3f, radius * 0.8f * scale, bend, 15f);
    }

    private void AddHead(Transform head, Rigidbody parent, float share, float mass, float scale)
    {
        Rigidbody body = AddBody(head, mass * share);
        var sphere = head.gameObject.AddComponent<SphereCollider>();
        sphere.radius = 0.11f * scale / Mathf.Max(0.0001f, head.lossyScale.x);
        Prepare(sphere);
        Joint(body, parent, 35f, 30f);
    }

    // A capsule from the bone to where its child begins, jointed to the part before it.
    private Rigidbody Part(Transform bone, Rigidbody parent, float share, float mass, Vector3 end, float radius, float twist = 0f, float swing = 0f)
    {
        Rigidbody body = AddBody(bone, mass * share);
        var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        Vector3 local = bone.InverseTransformPoint(end);
        Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        int axis = abs.x >= abs.y && abs.x >= abs.z ? 0 : abs.y >= abs.z ? 1 : 2;
        float unit = Mathf.Max(0.0001f, bone.lossyScale.x);
        capsule.direction = axis;
        capsule.center = local * 0.5f;
        capsule.height = local.magnitude + radius / unit;
        capsule.radius = radius / unit;
        Prepare(capsule);
        if (parent != null)
        {
            Joint(body, parent, twist, swing);
        }

        return body;
    }

    private static void Joint(Rigidbody body, Rigidbody parent, float twist, float swing)
    {
        var joint = body.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = parent;
        joint.enableProjection = true;
        joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
        joint.highTwistLimit = new SoftJointLimit { limit = twist };
        joint.swing1Limit = new SoftJointLimit { limit = swing };
        joint.swing2Limit = new SoftJointLimit { limit = swing };
    }

    // ----- A monster built from rigid parts -----

    // A skinned animal has no parts to go loose, and no ragdoll rig: its last pose becomes one body that falls.
    private void BakeSkins(Transform cutAt = null)
    {
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (skin.sharedMesh == null || OutlineCopies.IsCopy(skin))
            {
                continue;
            }

            // A cut: the part under cutAt becomes a piece of its own (jointed like any part; SeverPart frees it).
            if (cutAt != null && BodyCutter.Split(skin, cutAt, out Mesh cutPiece, out Mesh cutRest))
            {
                bakedMeshes.Add(cutPiece);
                bakedMeshes.Add(cutRest);
                foreach ((Mesh mesh, string label) in new[] { (cutRest, skin.name), (cutPiece, skin.name + " (cut off)") })
                {
                    var made = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
                    made.transform.SetParent(skin.transform.parent, false);
                    made.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    made.GetComponent<MeshFilter>().sharedMesh = mesh;
                    made.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                }

                skin.enabled = false;
                continue;
            }

            var baked = new Mesh { name = skin.sharedMesh.name + " (corpse)" };
            skin.BakeMesh(baked, true);
            bakedMeshes.Add(baked);
            var piece = new GameObject(skin.name, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(skin.transform.parent, false);
            piece.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            piece.GetComponent<MeshFilter>().sharedMesh = baked;
            piece.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
        }
    }

    private void BuildFromParts(Transform cutAt = null)
    {
        BakeSkins(cutAt);
        var parts = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
        // Neither the outline's copies nor the arrows stuck in it are parts of the body.
        parts.RemoveAll(part => OutlineCopies.IsCopy(part) || part.GetComponentInParent<ArrowProjectile>() != null);
        if (parts.Count == 0)
        {
            return;
        }

        // The biggest part is the body; every other hangs from it, which reads as limbs going loose.
        MeshRenderer main = parts[0];
        foreach (MeshRenderer part in parts)
        {
            if (Volume(part) > Volume(main))
            {
                main = part;
            }
        }

        Rigidbody trunk = null;
        float total = 0f;
        foreach (MeshRenderer part in parts)
        {
            total += Volume(part);
        }

        var pieces = new List<(MeshRenderer part, Rigidbody body)>(parts.Count);
        foreach (MeshRenderer part in parts)
        {
            // Flattened, so each piece moves alone; world pose and size stay as they are.
            part.transform.SetParent(transform, true);
            Rigidbody body = AddBody(part.transform, BodyMass * Mathf.Max(0.05f, Volume(part) / Mathf.Max(0.001f, total)) * Mathf.Clamp(total, 0.2f, 3f));
            var box = part.gameObject.AddComponent<BoxCollider>();
            if (part.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }

            Prepare(box);
            pieces.Add((part, body));
            if (part == main)
            {
                trunk = body;
            }
        }

        foreach ((MeshRenderer part, Rigidbody body) in pieces)
        {
            if (body != trunk)
            {
                Joint(body, trunk, 40f, 50f);
            }
        }
    }

    private static float Volume(Renderer renderer)
    {
        Vector3 size = renderer.bounds.size;
        return size.x * size.y * size.z;
    }

    // ----- All of them -----

    private Rigidbody AddBody(Transform bone, float mass)
    {
        var body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(0.5f, mass);
        body.linearDamping = 0.05f;
        body.angularDamping = 0.5f;
        body.solverIterations = 12;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        bone.gameObject.layer = IgnoreRaycastLayer;
        bodies.Add(body);
        return body;
    }

    private static void Prepare(Collider part)
    {
        if (Player.Current != null && Player.Current.TryGetComponent(out CharacterController controller))
        {
            Physics.IgnoreCollision(part, controller);
        }
    }

    private void Throw(in DamageInfo blow, float force)
    {
        Vector3 direction = blow.Direction;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : -transform.forward;
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Vector3 push = (direction + Vector3.up * feel.CorpseLift).normalized * Mathf.Lerp(feel.CorpseThrowMin, feel.CorpseThrowMax, force);
        Rigidbody struck = null;
        float nearest = float.MaxValue;
        foreach (Rigidbody body in bodies)
        {
            body.linearVelocity = push * Random.Range(0.85f, 1.1f);
            float distance = (body.worldCenterOfMass - blow.HitPoint).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                struck = body;
            }
        }

        // The part struck takes more of it, so the body folds around the blow instead of flying as one.
        if (struck != null)
        {
            struck.linearVelocity = push * 1.6f;
            struck.AddTorque(Vector3.Cross(Vector3.up, direction) * (force * 6f), ForceMode.VelocityChange);
        }

        wound = struck;

        settleAt = Time.time + feel.CorpseSettleSeconds;
        sinkAt = Time.time + feel.CorpseSeconds;
    }

    private void Register()
    {
        Corpses.RemoveAll(corpse => corpse == null);
        Corpses.Add(this);
        int most = CombatFeelSettings.Current.CorpseLimit;
        while (Corpses.Count > most)
        {
            if (Corpses[0] != null)
            {
                Destroy(Corpses[0].gameObject);
            }

            Corpses.RemoveAt(0);
        }
    }

    private void Update()
    {
        UpdateBleeding();
        if (!settled && Time.time >= settleAt)
        {
            settled = true;
            foreach (Rigidbody body in bodies)
            {
                if (body != null)
                {
                    body.isKinematic = true;
                    body.interpolation = RigidbodyInterpolation.None;
                }
            }
        }

        if (Time.time < sinkAt)
        {
            return;
        }

        transform.position += Vector3.down * (SinkSpeed * Time.deltaTime);
        if (Time.time >= sinkAt + SinkSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Corpses.Remove(this);
        foreach (Mesh baked in bakedMeshes)
        {
            Destroy(baked);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Corpses.Clear();
}
