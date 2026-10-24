using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Removes what a test created without leaving it to the Undo system. Setup tools record Undo steps on the
/// objects they touch (terrain data, added components); destroyed objects with Undo records can be brought back into the
/// open scene and saved with it (stray test terrains z-fighting with the real ground). So their Undo records are cleared
/// first, and scene objects are destroyed before the data they use.</summary>
public static class TestCleanup
{
    public static void Destroy(List<Object> created)
    {
        foreach (Object o in created.Where(o => o != null))
        {
            if (o is GameObject go)
            {
                foreach (Component component in go.GetComponentsInChildren<Component>(true))
                {
                    Undo.ClearUndo(component);
                }
            }

            Undo.ClearUndo(o);
        }

        foreach (Object o in created.Where(o => o is GameObject).Concat(created.Where(o => !(o is GameObject))).ToList())
        {
            if (o != null)
            {
                Object.DestroyImmediate(o);
            }
        }

        created.Clear();
    }
}
