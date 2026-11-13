using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The marks that set special places apart (BuildStructureModels.Marks): every raid event site and world prop has one, its
/// ring fits itself to the ground (so it paints no one walking through it), and the player has something to think there.
/// </summary>
public sealed class SiteMarkTests
{
    private static GameObject[] Prefabs(string folder) =>
        AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

    [Test]
    public void EveryEventSite_HasAMarkThatGoesOutWithIt()
    {
        foreach (GameObject site in Prefabs("Assets/_Project/Prefabs/Raids/Events/Sites").Where(p => p.GetComponent<RaidEventSite>() != null))
        {
            Transform mark = site.transform.Find("Mark");
            Assert.IsNotNull(mark, site.name);
            Assert.AreEqual(mark.gameObject, new SerializedObject(site.GetComponent<RaidEventSite>()).FindProperty("mark").objectReferenceValue, site.name);
            Assert.IsNotNull(mark.GetComponentInChildren<GroundRing>(true), $"{site.name}: the ring must fit itself to the ground");
            CuriousPlace place = mark.GetComponent<CuriousPlace>();
            Assert.IsNotNull(place, $"{site.name}: something to think there");
            Assert.IsNotEmpty(place.Thoughts, site.name);
            Assert.IsNull(mark.GetComponent<Interactable>(), $"{site.name}: a site's own use is its choices");
        }
    }

    [Test]
    public void EveryWorldProp_CanBeLookedAtCloser()
    {
        foreach (GameObject prop in Prefabs("Assets/_Project/Prefabs/World/Props"))
        {
            Assert.IsNotNull(prop.transform.Find("Mark"), prop.name);
            Assert.IsNotNull(prop.GetComponentInChildren<GroundRing>(true), prop.name);
            CuriousPlace place = prop.GetComponent<CuriousPlace>();
            Assert.IsNotNull(place, prop.name);
            Assert.GreaterOrEqual(place.Thoughts.Count, 2, $"{prop.name}: a closer look says something new");
            Assert.IsNotNull(prop.GetComponent<Interactable>(), $"{prop.name}: E to look closer");
            Assert.AreEqual("Look closer", place.Verb);
        }
    }
}
