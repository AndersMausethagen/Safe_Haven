using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>What a Content Editor section can ask of the window that shows it.</summary>
public interface IContentEditorHost
{
    ContentInspector Inspector { get; }
    IReadOnlyList<ContentSection> Sections { get; }
    void RefreshList();
    void Select(ContentSection section, Object asset);
    void Repaint();
    /// <summary>Opens the New form of a section with the draft prepared by initialize, e.g. a recipe for a given item.</summary>
    void CreateNew(ContentSection section, Action<ScriptableObject> initialize);
    /// <summary>The section that edits this asset, or null when no page shows it.</summary>
    ContentSection FindSectionFor(Object asset);
    /// <summary>Shows a short message over the window.</summary>
    void Notify(string message);
}

/// <summary>
/// One kind of content in the Content Editor: resources, monsters, attacks and so on. It says where the assets live,
/// how they are listed and checked, and adds its own panels (previews, prefab tools) to the details view. Creating,
/// duplicating and deleting work the same way for every kind.
/// <para>
/// Extending, to give a new kind of data asset its own page: subclass ContentSection (Title, Noun, SidebarGroup,
/// AssetType, Folder and Help are usually enough), add it to the list in ContentEditorWindow.EnsureSections, and override
/// FindProblems so its mistakes show up on the Needs Attention page. Override DrawExtras for previews or tools.
/// </para>
/// </summary>
public abstract class ContentSection
{
    private static readonly string[] NoFields = new string[0];

    public IContentEditorHost Host { get; internal set; }

    public abstract string Title { get; }
    /// <summary>One of these, e.g. "Resource" in "New Resource".</summary>
    public abstract string Noun { get; }
    /// <summary>Heading this section is listed under in the sidebar.</summary>
    public abstract string SidebarGroup { get; }
    public abstract Type AssetType { get; }
    /// <summary>Where new assets of this kind are saved. Null when they can't be created here.</summary>
    public virtual string Folder => null;
    /// <summary>A plain-language explanation shown when nothing is selected.</summary>
    public virtual string Help => null;
    /// <summary>Shown in the list when the section has no entries.</summary>
    public virtual string EmptyListText => $"No {Title.ToLowerInvariant()} yet.";
    public virtual bool CanCreate => Folder != null;
    public virtual bool CanDelete => true;
    public virtual bool CanDuplicate => CanCreate;
    /// <summary>False for sections that draw their own details instead of the asset's fields.</summary>
    public virtual bool UsesDataInspector => true;
    /// <summary>True when DrawSettings replaces the plain Settings panel (e.g. with tabs). Table mode and New still list fields.</summary>
    public virtual bool DrawsOwnSettings => false;
    /// <summary>Draws the selected asset's settings when DrawsOwnSettings. Returns true when something changed.</summary>
    public virtual bool DrawSettings(Object asset) => false;
    /// <summary>Fields drawn by the section's own panels rather than in the settings list.</summary>
    public virtual ICollection<string> HiddenFields => NoFields;
    /// <summary>Fields left out of the New form because a creation option sets them.</summary>
    protected virtual IEnumerable<string> CreateHiddenFields => NoFields;
    /// <summary>Fields pointing at generated prefabs; they are cleared on new and duplicated assets.</summary>
    protected virtual string[] LinkedPrefabFields => NoFields;

    public ICollection<string> HiddenWhenCreating
    {
        get
        {
            var hidden = new HashSet<string>(CreateHiddenFields) { "id", "displayName" };
            hidden.UnionWith(LinkedPrefabFields);
            return hidden;
        }
    }

    /// <summary>Releases editor resources such as cached inspectors when the window closes.</summary>
    public virtual void Dispose()
    {
    }

    public virtual List<Object> Load()
    {
        var assets = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + AssetType.Name))
        {
            Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), AssetType);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    public virtual string Label(Object asset) => ContentUtility.DisplayName(asset);

    /// <summary>True when this page edits the asset, so links to it (Connections, Needs Attention) open here.</summary>
    public virtual bool Handles(Object asset) => asset != null && asset.GetType() == AssetType;

    /// <summary>Heading the asset is listed under, e.g. its category. Null lists it without a heading.</summary>
    public virtual string ListGroup(Object asset) => null;

    public virtual Sprite ListIcon(Object asset) => null;

    public virtual Color? ListSwatch(Object asset) => null;

    /// <summary>The asset's tier, shown as a Roman numeral badge in the list. Null shows no badge.</summary>
    public virtual int? ListTier(Object asset) => null;

    public virtual int Compare(Object a, Object b)
    {
        int group = string.Compare(ListGroup(a) ?? "", ListGroup(b) ?? "", StringComparison.OrdinalIgnoreCase);
        return group != 0 ? group : string.Compare(Label(a), Label(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Things wrong with the asset, in plain language. Shown as a warning in the list and the details.</summary>
    public virtual List<string> FindProblems(Object asset, IList<Object> all)
    {
        var problems = new List<string>();
        string id = ContentUtility.IdOf(asset);
        if (id == null)
        {
            return problems;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            problems.Add("No id. Saves and lookups need one.");
            return problems;
        }

        foreach (Object other in all)
        {
            if (other != asset && other != null && ContentUtility.IdOf(other) == id)
            {
                problems.Add($"The id '{id}' is also used by {Label(other)}. Ids must be unique.");
            }
        }

        return problems;
    }

    /// <summary>Extra panels below the settings: previews, UI, prefab tools.</summary>
    public virtual void DrawExtras(Object asset)
    {
    }

    public ScriptableObject CreateDraft()
    {
        ScriptableObject draft = ScriptableObject.CreateInstance(AssetType);
        // DontSave, not HideAndDontSave: that one includes NotEditable, which greys out every field of the draft.
        draft.hideFlags = HideFlags.DontSave;
        InitializeDraft(draft);
        return draft;
    }

    /// <summary>Starting values for a new asset.</summary>
    protected virtual void InitializeDraft(ScriptableObject draft)
    {
    }

    /// <summary>Choices that only matter while creating, such as a model and whether to make a prefab.</summary>
    public virtual void DrawCreateOptions()
    {
    }

    /// <summary>Why the asset can't be created yet, or null when it can.</summary>
    public virtual string ValidateCreate(ScriptableObject draft, string name)
    {
        return null;
    }

    /// <summary>
    /// Runs after a new or duplicated asset is saved. withOptions is true when it came from the New form, so the
    /// choices made in DrawCreateOptions apply.
    /// </summary>
    protected virtual void AfterCreate(ScriptableObject asset, bool withOptions)
    {
    }

    /// <summary>Saves a copy of the draft as a new asset in this section's folder.</summary>
    public ScriptableObject CreateFromDraft(ScriptableObject draft, string name, bool withOptions)
    {
        var asset = (ScriptableObject)Object.Instantiate(draft);
        asset.hideFlags = HideFlags.None;
        string fileName = PrefabBuildUtility.MakeFileName(name);
        asset.name = fileName;

        var data = new SerializedObject(asset);
        SerializedProperty displayName = data.FindProperty("displayName");
        if (displayName != null)
        {
            displayName.stringValue = name.Trim();
        }

        SerializedProperty id = data.FindProperty("id");
        if (id != null)
        {
            id.stringValue = PrefabBuildUtility.MakeId(name);
        }

        ClearLinkedPrefabs(data);
        data.ApplyModifiedPropertiesWithoutUndo();

        PrefabBuildUtility.EnsureFolder(Folder);
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{fileName}.asset"));
        AfterCreate(asset, withOptions);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Creates an asset with default values, for the "New" buttons beside data fields.</summary>
    public ScriptableObject CreateNamed(string name)
    {
        ScriptableObject draft = CreateDraft();
        try
        {
            return CreateFromDraft(draft, name, false);
        }
        finally
        {
            Object.DestroyImmediate(draft);
        }
    }

    /// <summary>Copies the asset next to the original with a new name and id, without its generated prefab.</summary>
    public virtual Object Duplicate(Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        string copyName = Label(asset) + " Copy";
        string copyPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{Path.GetDirectoryName(path).Replace('\\', '/')}/{PrefabBuildUtility.MakeFileName(copyName)}.asset");
        if (!AssetDatabase.CopyAsset(path, copyPath))
        {
            throw new InvalidOperationException($"Unity could not copy {path}.");
        }

        var copy = (ScriptableObject)AssetDatabase.LoadAssetAtPath(copyPath, AssetType);
        var data = new SerializedObject(copy);
        SerializedProperty displayName = data.FindProperty("displayName");
        if (displayName != null)
        {
            displayName.stringValue = copyName;
        }

        SerializedProperty id = data.FindProperty("id");
        if (id != null)
        {
            id.stringValue = PrefabBuildUtility.MakeId(copyName);
        }

        ClearLinkedPrefabs(data);
        data.ApplyModifiedPropertiesWithoutUndo();
        AfterCreate(copy, false);
        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();
        return copy;
    }

    /// <summary>Why the asset can't be deleted right now, or null when it can.</summary>
    public virtual string DeleteBlocker(Object asset) => null;

    /// <summary>References that don't need a warning because they are cleaned up automatically.</summary>
    public virtual bool IgnoreReference(string path) => false;

    public virtual void BeforeDelete(Object asset)
    {
    }

    /// <summary>The generated prefab that belongs to the asset, offered for deletion with it.</summary>
    public virtual GameObject LinkedPrefab(Object asset) => null;

    private void ClearLinkedPrefabs(SerializedObject data)
    {
        foreach (string field in LinkedPrefabFields)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = null;
            }
        }
    }
}
