using UnityEngine;

/// <summary>
/// Where a held model's ends are: found from its meshes, along its longest axis, in the model's own space. The tip is the end further
/// from the hand. The blade trail runs along it and the left hand takes hold of it.
/// </summary>
public static class HeldWeaponShape
{
    /// <summary>False for a model with no meshes.</summary>
    public static bool Measure(GameObject model, Transform grip, out Vector3 tip, out Vector3 butt)
    {
        tip = butt = Vector3.zero;
        bool found = false;
        var bounds = new Bounds();
        Matrix4x4 toModel = model.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 matrix = toModel * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        if (!found)
        {
            return false;
        }

        Vector3 size = bounds.size;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        Vector3 along = Vector3.zero;
        along[axis] = size[axis] * 0.5f;
        Vector3 one = bounds.center + along, other = bounds.center - along;
        Vector3 hand = grip != null ? model.transform.InverseTransformPoint(grip.position) : Vector3.zero;
        bool oneIsTip = (one - hand).sqrMagnitude >= (other - hand).sqrMagnitude;
        tip = oneIsTip ? one : other;
        butt = oneIsTip ? other : one;
        return true;
    }
}
