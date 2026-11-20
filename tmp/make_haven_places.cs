const string lib = "Assets/_Project/Audio/Sounds/";
var log = new System.Text.StringBuilder();
SoundDefinition Copy(string from, string name, float reach)
{
    string path = lib + "Places/" + name + ".asset";
    if (!UnityEditor.AssetDatabase.IsValidFolder(lib + "Places")) UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Audio/Sounds", "Places");
    var sound = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
    if (sound == null)
    {
        sound = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinition>(lib + from + ".asset"));
        UnityEditor.AssetDatabase.CreateAsset(sound, path);
    }
    var so = new UnityEditor.SerializedObject(sound);
    so.FindProperty("displayName").stringValue = name;
    so.FindProperty("audibleDistance").floatValue = reach;
    so.FindProperty("positional").boolValue = true;
    so.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.EditorUtility.SetDirty(sound);
    log.Append(name + " " + reach + "m; ");
    return sound;
}
var crows = Copy("Library/Crow Caw", "Place Crows", 80f);
var howl = Copy("Creatures/Wolf Howl", "Place Howl", 90f);
var raven = Copy("Biomes/Valley Raven", "Place Raven", 80f);
HavenPlaceVerbs.Kind K(string kind, params string[] thoughts) => new HavenPlaceVerbs.Kind { kind = kind, thoughts = thoughts };
var kinds = new System.Collections.Generic.List<HavenPlaceVerbs.Kind> {
  K("Wayfarer's Stones", "Four stones leaning together, worn smooth where hands rested on them. Travellers stopped here, once.",
    "A line is cut into the tallest: a road, running toward the clearing. Toward the portal."),
  K("Island Stones", "Stones out in the water, set there on purpose. Whoever raised them wanted them hard to reach.",
    "Moss on the landward side only. Whatever they were for, they faced the shore."),
  K("Drowned Stones", "Half of them under the water now. The lake has come up since they were set.",
    "The water here is too still. Something about it makes me want to keep walking."),
  K("Fisher's Rest", "Old drying poles, and a fire-ring black with fish fat. They ate well here, for a while.",
    "A net, rotted to strings on its pegs. Nobody came back for it."),
  K("Mother Maple", "The oldest tree I've seen this side of the portal. Its roots have swallowed a stone wall whole.",
    "Scratches in the bark at head height, in rows. Someone was counting something, a long time ago."),
  K("Shepherd's Fold", "A ring of low walls for sheep. No sheep now; the grass has the place.",
    "The gate faces the clearing. They brought the flock in toward the light at night."),
  new HavenPlaceVerbs.Kind { kind = "Ruined Watchtower", lookout = true, thoughts = new[] {
    "Someone watched the woods from here. The top has come down, but the stair still climbs.",
    "Arrow-slits on the forest side only. They knew which way it would come from." } },
  new HavenPlaceVerbs.Kind { kind = "Standing Stones", lookout = true, thoughts = new[] {
    "A ring of standing stones, older than anything else out here. The carvings are worn almost flat.",
    "Nothing grows inside the ring. Not moss, not grass." } },
  new HavenPlaceVerbs.Kind { kind = "Lookout Cairn", lookout = true, sound = raven, soundWhen = HavenPlaceVerbs.When.Day, soundEvery = new Vector2(18f, 35f), thoughts = new[] {
    "A cairn on the height, stacked by many hands, one stone at a time.",
    "From up here the whole land lies open. Worth the climb." } },
  new HavenPlaceVerbs.Kind { kind = "Old Barrow", sound = crows, soundWhen = HavenPlaceVerbs.When.Day, soundEvery = new Vector2(10f, 25f), thoughts = new[] {
    "A long mound, grassed over, with a stone door in its side. A grave for someone who mattered.",
    "The door was shut from the outside. And sealed again since, by someone else." } },
  new HavenPlaceVerbs.Kind { kind = "Burnt Farmstead", sound = crows, soundWhen = HavenPlaceVerbs.When.Day, soundEvery = new Vector2(12f, 28f), thoughts = new[] {
    "The hearthstone is cracked right through. Somebody tried to keep this fire going after the roof was gone.",
    "No bones. Whoever lived here walked away from it, or was taken." } },
  new HavenPlaceVerbs.Kind { kind = "Wolf Den", sound = howl, soundWhen = HavenPlaceVerbs.When.Night, soundEvery = new Vector2(30f, 60f), thoughts = new[] {
    "A den under the rock-fall. The smell alone says keep back.",
    "Gnawed bones at its mouth. Not all of them deer." } },
  K("Old Quarry", "Old cuttings in the rock, the marks of the wedges still in the stone. Masons worked here.",
    "Some of the cut blocks were never carried away. As if they stopped in the middle of a day."),
  K("Elder Cedar", "A cedar older than the Haven. The air is cooler under it, and quiet.",
    "Small things tucked into the bark: a bead, a twist of cloth. Offerings, or thanks."),
  K("Woodland Camp", "A camp in the trees: a fire-ring, a lean-to rotted down to its frame.",
    "They left in a hurry. The pot is still on its hook."),
};
var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/StoneBlock.asset");
var granite = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/GraniteBlock.asset");
var finds = new System.Collections.Generic.List<HavenPlaceVerbs.Find> {
  new HavenPlaceVerbs.Find { placeId = "haven.old_quarry.0", name = "Cut stone", verb = "Take", item = stone, amount = 6, offset = new Vector2(3f, -3f),
    taken = "Squared blocks, cut and never carried home. They'll do for the altar." },
  new HavenPlaceVerbs.Find { placeId = "haven.old_quarry.1", name = "Cut granite", verb = "Take", item = granite, amount = 4, offset = new Vector2(3f, -3f),
    taken = "Granite, dressed on every face. Whoever cut this meant to build something that would last." },
};
const string verbsPath = "Assets/_Project/Resources/HavenPlaceVerbs.asset";
var verbs = UnityEditor.AssetDatabase.LoadAssetAtPath<HavenPlaceVerbs>(verbsPath);
if (verbs == null) { verbs = ScriptableObject.CreateInstance<HavenPlaceVerbs>(); UnityEditor.AssetDatabase.CreateAsset(verbs, verbsPath); }
verbs.Set(kinds, finds);
UnityEditor.EditorUtility.SetDirty(verbs);
var territory = TerritorySettings.Current;
var tso = new UnityEditor.SerializedObject(territory);
tso.FindProperty("findInsight").intValue = 10;
var regions = tso.FindProperty("regions");
for (int i = 0; i < regions.arraySize; i++)
{
    var r = regions.GetArrayElementAtIndex(i);
    string name = r.FindPropertyRelative("region").stringValue;
    if (name == "Ravenwood") { r.FindPropertyRelative("danger").enumValueIndex = 2; r.FindPropertyRelative("gives").stringValue = "Hardwood, iron, stone, game"; log.Append("Ravenwood; "); }
    if (name == "The Wolfwood") { r.FindPropertyRelative("danger").enumValueIndex = 2; r.FindPropertyRelative("gives").stringValue = "Fangs, hides; the Wolf Den and the Old Barrow"; log.Append("Wolfwood; "); }
    if (name == "Greyfell") { r.FindPropertyRelative("danger").enumValueIndex = 1; r.FindPropertyRelative("gives").stringValue = "Stone, granite, copper and iron ore. Nothing lives up there but the wind."; log.Append("Greyfell; "); }
}
tso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.EditorUtility.SetDirty(territory);
UnityEditor.AssetDatabase.SaveAssets();
return log + " kinds " + verbs.Kinds.Count + " finds " + verbs.Finds.Count + " stone " + (stone != null) + " granite " + (granite != null) + " insight base " + territory.FindInsight + " at 290 m " + territory.FindInsightAt(290f);
