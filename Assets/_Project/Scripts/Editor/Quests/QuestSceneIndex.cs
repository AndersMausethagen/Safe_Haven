using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>What a scene or prefab object does with a quest or Quest Event.</summary>
public enum QuestSceneRole
{
    /// <summary>A Quest Giver for the quest.</summary>
    Gives,
    /// <summary>A Quest Gate locked by the quest.</summary>
    Gates,
    /// <summary>A Quest Event Trigger that raises the event.</summary>
    Raises,
    /// <summary>A Quest Event Trigger that only works while the quest is active.</summary>
    LimitedTo,
    /// <summary>A Quest Event Listener for the event or quest.</summary>
    Listens,
    /// <summary>Any other reference, e.g. an Inspector event that calls Raise. Treated as raising an event.</summary>
    Other,
    /// <summary>An override on a prefab instance, so the component type isn't known.</summary>
    PrefabOverride,
    /// <summary>A story's Start Quest node, which starts the quest like a Quest Giver would.</summary>
    Starts,
    /// <summary>The object is this person, place or thing for quests (a [QuestIdentity] field: a zone's Target, a
    /// Talkable's Quest Target, an expedition member's resident).</summary>
    Is,
}

/// <summary>One scene or prefab object that uses a quest or Quest Event.</summary>
public sealed class QuestSceneUse
{
    public string AssetPath;
    public string ObjectName;
    public long ObjectFileId;
    public QuestSceneRole Role;
    public string TargetGuid;
    /// <summary>The component that uses it (a Talkable, a Quest Event Trigger...); null for a prefab override.</summary>
    public Type Component;
    /// <summary>For a story: the node that uses it (WhenQuestEvent, HasHappenedNode...); null otherwise.</summary>
    public string StoryNode;

    public bool IsScene => AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

    public bool IsDataAsset => AssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);

    public string Describe()
    {
        string where = Path.GetFileNameWithoutExtension(AssetPath);
        if (IsDataAsset)
        {
            return $"{where}  (asset)";
        }

        return $"{ObjectName}  ({where}{(IsScene ? "" : " prefab")})";
    }
}

/// <summary>
/// Which saved scenes and prefabs give, gate, raise or listen for each quest and Quest Event, so the Content Editor can
/// show both directions ("Given by the Smith in SafeHaven") and warn about loose ends (a By Quest Giver quest nobody
/// gives). It reads the scene and prefab files as text, so no scene has to be opened; unsaved changes aren't seen. The
/// result is cached until the project changes or a scene is saved.
/// </summary>
public static class QuestSceneIndex
{
    private static readonly Regex HeaderPattern = new Regex(@"^(\d+) &(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex NamePattern = new Regex(@"^  m_Name: (.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex OwnerPattern = new Regex(@"^  m_GameObject: \{fileID: (-?\d+)\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ScriptPattern = new Regex(@"^  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ReferencePattern = new Regex(@"^( *)(?:- )?(\w+): \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 2\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex OverridePattern = new Regex(@"propertyPath: (\S+)\s+value:[^\n]*\n\s+objectReference: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 2\}", RegexOptions.Compiled);
    private static readonly Regex StoryNodePattern = new Regex(@"^ *type: \{class: (\w+),", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex InstanceOfPattern = new Regex(@"^  m_PrefabInstance: \{fileID: (-?\d+)\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex InstanceNamePattern = new Regex(@"propertyPath: m_Name\s+value: (.+)", RegexOptions.Compiled);
    private static readonly Regex SourcePattern = new Regex(@"^  m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled | RegexOptions.Multiline);

    private static List<QuestSceneUse> cached;
    private static Dictionary<Type, HashSet<string>> raisingFields;
    private static Dictionary<Type, HashSet<string>> identityFields;

    /// <summary>Every field marked [QuestIdentity] (who or what its object is for quests), by declaring type.</summary>
    public static IReadOnlyDictionary<Type, HashSet<string>> IdentityFields
    {
        get
        {
            if (identityFields == null)
            {
                identityFields = new Dictionary<Type, HashSet<string>>();
                foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<QuestIdentityAttribute>())
                {
                    if (!identityFields.TryGetValue(field.DeclaringType, out HashSet<string> names))
                    {
                        identityFields[field.DeclaringType] = names = new HashSet<string>();
                    }

                    names.Add(field.Name);
                }
            }

            return identityFields;
        }
    }

    /// <summary>Every field marked [RaisedByGame], by the type that declares it.</summary>
    public static IReadOnlyDictionary<Type, HashSet<string>> RaisingFields
    {
        get
        {
            if (raisingFields == null)
            {
                raisingFields = new Dictionary<Type, HashSet<string>>();
                foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<RaisedByGameAttribute>())
                {
                    if (!raisingFields.TryGetValue(field.DeclaringType, out HashSet<string> names))
                    {
                        raisingFields[field.DeclaringType] = names = new HashSet<string>();
                    }

                    names.Add(field.Name);
                }
            }

            return raisingFields;
        }
    }

    static QuestSceneIndex()
    {
        EditorApplication.projectChanged += Invalidate;
        EditorSceneManager.sceneSaved += _ => Invalidate();
    }

    public static void Invalidate()
    {
        cached = null;
    }

    /// <summary>Every scene and prefab use of the asset.</summary>
    public static List<QuestSceneUse> UsesOf(Object asset)
    {
        var found = new List<QuestSceneUse>();
        if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
        {
            return found;
        }

        foreach (QuestSceneUse use in All())
        {
            if (use.TargetGuid == guid)
            {
                found.Add(use);
            }
        }

        return found;
    }

    /// <summary>Whether anything in a scene, prefab or quest raises the Quest Event.</summary>
    public static bool IsRaised(GameEventDefinition gameEvent, IEnumerable<QuestDefinition> quests)
    {
        foreach (QuestDefinition quest in quests)
        {
            if (quest != null && (quest.CompletionEvent == gameEvent ||
                System.Linq.Enumerable.Contains(quest.OnStarted, gameEvent) ||
                System.Linq.Enumerable.Contains(quest.OnCompleted, gameEvent)))
            {
                return true;
            }
        }

        foreach (QuestSceneUse use in UsesOf(gameEvent))
        {
            if (use.Role == QuestSceneRole.Raises || use.Role == QuestSceneRole.Other || use.Role == QuestSceneRole.PrefabOverride)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a standard giver or potential giver prefab override offers the quest.</summary>
    public static bool HasGiver(QuestDefinition quest)
    {
        foreach (QuestSceneUse use in UsesOf(quest))
        {
            if (use.Role == QuestSceneRole.Gives || use.Role == QuestSceneRole.PrefabOverride)
            {
                return true;
            }
        }

        return GivingResident(quest) != null;
    }

    /// <summary>The resident whose quests at home offer it (ResidentDefinition › Quest Line or Side Quests: a QuestGiver made on them),
    /// or null.</summary>
    public static ResidentDefinition GivingResident(QuestDefinition quest)
    {
        if (quest == null)
        {
            return null;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ResidentDefinition"))
        {
            var resident = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (resident != null && (Offers(resident.QuestLine, quest) || Offers(resident.SideQuests, quest)))
            {
                return resident;
            }
        }

        return null;
    }

    private static bool Offers(IReadOnlyList<QuestOffer> offers, QuestDefinition quest) =>
        offers != null && System.Linq.Enumerable.Any(offers, offer => offer != null && offer.Quest == quest);

    /// <summary>Saved scene and prefab objects that are this person, place or thing for quests.</summary>
    public static List<QuestSceneUse> MarkedAs(UnityEngine.Object identity)
    {
        return UsesOf(identity).FindAll(use => use.Role == QuestSceneRole.Is);
    }

    /// <summary>Whether a story starts the quest with a Start Quest node.</summary>
    public static bool IsStartedByStory(QuestDefinition quest)
    {
        foreach (QuestSceneUse use in UsesOf(quest))
        {
            if (use.Role == QuestSceneRole.Starts)
            {
                return true;
            }
        }

        return false;
    }

    public static List<QuestSceneUse> All()
    {
        if (cached != null)
        {
            return cached;
        }

        var scripts = new Dictionary<string, Type>
        {
            [ScriptGuid(typeof(QuestGiver))] = typeof(QuestGiver),
            [ScriptGuid(typeof(QuestGate))] = typeof(QuestGate),
            [ScriptGuid(typeof(GameEventTrigger))] = typeof(GameEventTrigger),
            [ScriptGuid(typeof(GameEventListener))] = typeof(GameEventListener),
            [ScriptGuid(typeof(StoryScript))] = typeof(StoryScript),
        };

        var targets = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition t:GameEventDefinition t:QuestTargetDefinition t:ResidentDefinition"))
        {
            targets.Add(guid);
        }

        // Components whose fields say who or what they are for quests.
        foreach (Type marked in IdentityFields.Keys)
        {
            if (typeof(MonoBehaviour).IsAssignableFrom(marked))
            {
                scripts[ScriptGuid(marked)] = marked;
            }
        }

        // Components and data assets with [RaisedByGame] Quest Event fields (a traveler being freed, a job being assigned).
        string search = "t:Scene t:Prefab";
        foreach (Type raiser in RaisingFields.Keys)
        {
            scripts[ScriptGuid(raiser)] = raiser;
            if (typeof(ScriptableObject).IsAssignableFrom(raiser))
            {
                search += " t:" + raiser.Name;
            }
        }

        scripts.Remove("");

        cached = new List<QuestSceneUse>();

        foreach (string guid in AssetDatabase.FindAssets(search, new[] { ContentUtility.SearchRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            try
            {
                cached.AddRange(Parse(File.ReadAllText(path), path, scripts, targets));
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Quest index: could not read {path}: {exception.Message}");
            }
        }

        return cached;
    }

    /// <summary>
    /// Reads one scene or prefab file. Scripts maps the quest components' script GUIDs to their types; only references to
    /// GUIDs in targets (quests and Quest Events) are kept.
    /// </summary>
    public static List<QuestSceneUse> Parse(string text, string path, IReadOnlyDictionary<string, Type> scripts, ICollection<string> targets)
    {
        var uses = new List<QuestSceneUse>();
        var names = new Dictionary<long, string>();
        // A prefab instance's objects are written "stripped", without a name: it is the instance's (its m_Name override,
        // or its prefab's file name).
        var strippedOf = new Dictionary<long, long>();
        var instanceNames = new Dictionary<long, string>();
        string[] documents = text.Split(new[] { "\n--- !u!" }, StringSplitOptions.None);
        foreach (string document in documents)
        {
            Match header = HeaderPattern.Match(document);
            if (!header.Success)
            {
                continue;
            }

            string classId = header.Groups[1].Value;
            long fileId = long.Parse(header.Groups[2].Value);
            if (classId == "1")
            {
                Match name = NamePattern.Match(document);
                names[fileId] = name.Success ? name.Groups[1].Value.Trim() : "(unnamed)";
                Match instance = name.Success ? Match.Empty : InstanceOfPattern.Match(document);
                if (instance.Success)
                {
                    strippedOf[fileId] = long.Parse(instance.Groups[1].Value);
                }
            }
            else if (classId == "114")
            {
                ReadComponent(document, path, scripts, targets, uses);
            }
            else if (classId == "1001")
            {
                Match renamed = InstanceNamePattern.Match(document);
                Match source = SourcePattern.Match(document);
                if (renamed.Success)
                {
                    instanceNames[fileId] = renamed.Groups[1].Value.Trim();
                }
                else if (source.Success)
                {
                    instanceNames[fileId] = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(source.Groups[1].Value));
                }

                foreach (Match change in OverridePattern.Matches(document))
                {
                    if (targets.Contains(change.Groups[2].Value))
                    {
                        uses.Add(new QuestSceneUse
                        {
                            AssetPath = path,
                            ObjectName = "Prefab instance override",
                            Role = QuestSceneRole.PrefabOverride,
                            TargetGuid = change.Groups[2].Value,
                        });
                    }
                }
            }
        }

        foreach (KeyValuePair<long, long> stripped in strippedOf)
        {
            if (instanceNames.TryGetValue(stripped.Value, out string instanceName) && !string.IsNullOrEmpty(instanceName))
            {
                names[stripped.Key] = instanceName;
            }
        }

        foreach (QuestSceneUse use in uses)
        {
            if (use.Role != QuestSceneRole.PrefabOverride)
            {
                use.ObjectName = names.TryGetValue(use.ObjectFileId, out string name) ? name : "(unknown object)";
            }
        }

        return uses;
    }

    private static void ReadComponent(string document, string path, IReadOnlyDictionary<string, Type> scripts, ICollection<string> targets,
        List<QuestSceneUse> uses)
    {
        Match owner = OwnerPattern.Match(document);
        Match script = ScriptPattern.Match(document);
        long ownerId = owner.Success ? long.Parse(owner.Groups[1].Value) : 0;
        Type type = script.Success && scripts.TryGetValue(script.Groups[1].Value, out Type known) ? known : null;
        foreach (Match reference in ReferencePattern.Matches(document))
        {
            string guid = reference.Groups[3].Value;
            if (!targets.Contains(guid))
            {
                continue;
            }

            bool topLevel = reference.Groups[1].Value.Length == 2;
            // A list's first member can be on the "- quest:" line or indented below another member.
            bool orderedOffer = type == typeof(QuestGiver) && reference.Groups[2].Value == "quest" &&
                (RootFieldAt(document, reference.Index) == "offers" || RootFieldAt(document, reference.Index) == "sideOffers");
            string node = type == typeof(StoryScript) ? NodeAt(document, reference.Index) : null;
            QuestSceneRole role = type == typeof(StoryScript) ? RoleOfStoryNode(node)
                : topLevel || orderedOffer ? RoleOf(type, reference.Groups[2].Value) : QuestSceneRole.Other;
            uses.Add(new QuestSceneUse
            {
                AssetPath = path,
                ObjectFileId = ownerId,
                Role = role,
                TargetGuid = guid,
                Component = type,
                StoryNode = node,
            });
        }
    }

    // A story's nodes are written below it, each after a "type: {class: SayNode, ...}" line.
    private static string NodeAt(string document, int index)
    {
        string node = null;
        foreach (Match match in StoryNodePattern.Matches(document.Substring(0, index)))
        {
            node = match.Groups[1].Value;
        }

        return node;
    }

    private static QuestSceneRole RoleOfStoryNode(string node)
    {
        switch (node)
        {
            case nameof(StartQuestNode):
                return QuestSceneRole.Starts;
            case nameof(RaiseQuestEventNode):
                return QuestSceneRole.Raises;
            case nameof(WhenQuest):
            case nameof(WhenQuestEvent):
            case nameof(CheckQuestNode):
            case nameof(HasHappenedNode):
            // Waits for the event: it raises nothing, so it must not make an event look raised.
            case nameof(WaitForEventNode):
                return QuestSceneRole.Listens;
            default:
                return QuestSceneRole.Other;
        }
    }

    private static string RootFieldAt(string document, int index)
    {
        string field = null;
        foreach (Match match in Regex.Matches(document.Substring(0, index), @"^  (\w+):", RegexOptions.Multiline))
            field = match.Groups[1].Value;
        return field;
    }

    private static QuestSceneRole RoleOf(Type type, string field)
    {
        if (type == typeof(QuestGiver) && field == "quest")
        {
            return QuestSceneRole.Gives;
        }

        if (type == typeof(QuestGate) && field == "quest")
        {
            return QuestSceneRole.Gates;
        }

        if (type == typeof(GameEventTrigger) && field == "onlyDuringQuest")
        {
            return QuestSceneRole.LimitedTo;
        }

        if (type != null && IdentityFields.TryGetValue(type, out HashSet<string> identity) && identity.Contains(field))
        {
            return QuestSceneRole.Is;
        }

        if (type != null && RaisingFields.TryGetValue(type, out HashSet<string> raised) && raised.Contains(field))
        {
            return QuestSceneRole.Raises;
        }

        return type == typeof(GameEventListener) ? QuestSceneRole.Listens : QuestSceneRole.Other;
    }

    private static string ScriptGuid(Type type)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{type.Name} t:MonoScript"))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
            if (script != null && script.GetClass() == type)
            {
                return guid;
            }
        }

        return "";
    }

    /// <summary>Opens the scene (asking to save the current one) and selects the object, or reveals the prefab.</summary>
    public static void Open(QuestSceneUse use)
    {
        if (!use.IsScene)
        {
            ContentUtility.Reveal(AssetDatabase.LoadMainAssetAtPath(use.AssetPath));
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Debug.Log($"Stop play mode to open {use.AssetPath}.");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != use.AssetPath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(use.AssetPath);
        }

        string id = $"GlobalObjectId_V1-2-{AssetDatabase.AssetPathToGUID(use.AssetPath)}-{use.ObjectFileId}-0";
        Object target = GlobalObjectId.TryParse(id, out GlobalObjectId globalId) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) : null;
        if (target == null)
        {
            target = GameObject.Find(use.ObjectName);
        }

        if (target != null)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            SceneView.FrameLastActiveSceneView();
        }
    }

    /// <summary>Every GameObject in the loaded scenes with a component that references the asset, including unsaved edits and Inspector events.</summary>
    public static List<GameObject> FindInOpenScenes(Object asset)
    {
        var found = new List<GameObject>();
        if (asset == null)
        {
            return found;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && !found.Contains(behaviour.gameObject) &&
                        References(behaviour, asset))
                    {
                        found.Add(behaviour.gameObject);
                    }
                }
            }
        }

        return found;
    }

    private static bool References(Object component, Object asset)
    {
        using (var data = new SerializedObject(component))
        {
            SerializedProperty property = data.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == asset)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
