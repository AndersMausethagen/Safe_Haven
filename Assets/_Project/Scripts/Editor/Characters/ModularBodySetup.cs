using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts a modular body (ModularCharacter, with CharacterEmotes) on a humanoid in place of its old model: the old body's
/// children are removed, the new body built, and every serialized reference other components held to a bone of the old
/// body is pointed at the same humanoid bone of the new one. Used by PlayerBodySetup and PeopleSetup.
/// </summary>
public static class ModularBodySetup
{
    private struct BoneReference
    {
        public Object Owner;
        public string Path;
        public HumanBodyBones Bone;
        public string Name;
        public bool AsGameObject;
    }

    /// <summary>Gives <paramref name="root"/> (the object with the Animator) a modular body. Returns a summary.</summary>
    public static string Apply(GameObject root, CharacterAppearance look, IEnumerable<GearLookDefinition> outfit, IEnumerable<Transform> oldParts,
        RuntimeAnimatorController controller = null)
    {
        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }

        List<Transform> old = oldParts.Where(p => p != null).Distinct().ToList();
        List<BoneReference> references = CollectReferences(root, animator, old);
        foreach (Transform part in old)
        {
            Object.DestroyImmediate(part.gameObject);
        }

        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }

        animator.applyRootMotion = false;
        ModularCharacter character = root.GetComponent<ModularCharacter>();
        bool rebuilt = character != null;
        if (character == null)
        {
            character = root.AddComponent<ModularCharacter>();
        }

        if (root.GetComponent<CharacterEmotes>() == null)
        {
            root.AddComponent<CharacterEmotes>();
        }

        character.SetAppearance(look);
        character.SetOutfit(outfit);
        if (rebuilt)
        {
            character.Build(true);
        }

        int remapped = Remap(references, animator, character.Skeleton);
        EditorUtility.SetDirty(root);
        return $"{root.name}: {look.gender} body, {references.Count} bone references ({remapped} remapped).";
    }

    private static List<BoneReference> CollectReferences(GameObject root, Animator animator, List<Transform> old)
    {
        var references = new List<BoneReference>();
        if (old.Count == 0)
        {
            return references;
        }

        var humanBones = new Dictionary<Transform, HumanBodyBones>();
        if (animator.isHuman)
        {
            for (HumanBodyBones bone = 0; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t != null && !humanBones.ContainsKey(t))
                {
                    humanBones.Add(t, bone);
                }
            }
        }

        bool InOld(Transform t) => t != null && old.Any(p => t == p || t.IsChildOf(p));
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || InOld(component.transform) || component is Transform)
            {
                continue;
            }

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                {
                    continue;
                }

                Object value = property.objectReferenceValue;
                Transform target = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
                if (!InOld(target) || value is Renderer)
                {
                    continue;
                }

                references.Add(new BoneReference
                {
                    Owner = component,
                    Path = property.propertyPath,
                    Bone = humanBones.TryGetValue(target, out HumanBodyBones bone) ? bone : HumanBodyBones.LastBone,
                    Name = target.name,
                    AsGameObject = value is GameObject,
                });
            }
        }

        return references;
    }

    private static int Remap(List<BoneReference> references, Animator animator, Transform skeleton)
    {
        int remapped = 0;
        var problems = new StringBuilder();
        foreach (BoneReference reference in references)
        {
            Transform target = reference.Bone != HumanBodyBones.LastBone ? animator.GetBoneTransform(reference.Bone) : null;
            if (target == null && skeleton != null)
            {
                target = skeleton.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == reference.Name);
            }

            var serialized = new SerializedObject(reference.Owner);
            SerializedProperty property = serialized.FindProperty(reference.Path);
            if (property == null)
            {
                continue;
            }

            if (target == null)
            {
                problems.Append(reference.Owner.GetType().Name).Append('.').Append(reference.Path).Append(" (").Append(reference.Name).Append(") ");
                continue;
            }

            property.objectReferenceValue = reference.AsGameObject ? target.gameObject : target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            remapped++;
        }

        if (problems.Length > 0)
        {
            Debug.LogWarning("Modular body: no new bone for " + problems);
        }

        return remapped;
    }
}
