using System;
using System.Collections.Generic;

/// <summary>
/// The file the Safe Haven Progression Planner writes and the Unity importer reads (Safe Haven > Planner > Import Planner
/// Data). Plain serializable classes so JsonUtility can read it. Values travel as text, lists as lines; references are
/// "kind:id" keys, never GUIDs, so the planner can point at assets that do not exist yet.
/// </summary>
[Serializable]
public sealed class PlannerManifest
{
    public int version;
    public string generatedAt;
    public List<PlannerEntity> entities = new List<PlannerEntity>();
}

[Serializable]
public sealed class PlannerEntity
{
    public string kind;
    public string key;
    public string id;
    /// <summary>Set for assets that already exist in Unity; the importer matches on it first, then on key.</summary>
    public string guid;
    /// <summary>Where a new asset is created. Never used to overwrite an existing file.</summary>
    public string path;
    public string typeName;
    public bool isNew;
    /// <summary>Only the fields the planner wants set: for existing assets just the ones that were edited.</summary>
    public List<PlannerField> fields = new List<PlannerField>();
}

[Serializable]
public sealed class PlannerField
{
    public string name;
    /// <summary>int float bool string text enum ref amounts drops lookup.</summary>
    public string ftype;
    /// <summary>Written only when the asset is created.</summary>
    public bool createOnly;
    public string value;
    public List<PlannerLine> lines = new List<PlannerLine>();
}

[Serializable]
public sealed class PlannerLine
{
    public string target;
    public int amount;
    public int min;
    public int max;
    public float prob;
}
