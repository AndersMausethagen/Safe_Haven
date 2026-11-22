using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The mist's wake field (MistWakes, Shaders/MistWakeSim.shader), run for real on the GPU and read back: a body parts the mist where it
/// stands, a wall in the square is solid and keeps the mist off it, and the rest of the square is left alone (the canary: a field read
/// without its textures bound once parted all of it). Blows throw the mist the way they go.
/// </summary>
public sealed class MistWakesTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private GameObject field;
    private GameObject wall;
    private GameObject mist;
    private MistWakes wakes;

    [SetUp]
    public void SetUp()
    {
        Assert.IsNotNull(MistWakeSettings.Current.Simulation, "the wake settings name the simulation shader, so builds include it");
        // Far from anything in the open scene: no terrain, no trees, nothing in the square but the wall.
        Vector3 focus = new Vector3(5000f, 0f, 5000f);
        // Some mist showing, or the field rests.
        mist = new GameObject("Mist Wakes test mist");
        mist.transform.position = focus;
        mist.AddComponent<MistVolume>();
        Assert.Greater(mist.GetComponent<MistVolume>().EffectiveDensity, 0f, "the test's mist shows");
        field = new GameObject("Mist Wakes test");
        field.transform.position = focus;
        wakes = field.AddComponent<MistWakes>();
        Call("Awake");
        wakes.Follow = field.transform;
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Mist Wakes test wall";
        // Off the body's row as well as its column, so a field turned or flipped in either axis fails.
        wall.transform.position = focus + new Vector3(6f, 1f, 5f);
        wall.transform.localScale = new Vector3(2f, 2f, 2f);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        if (wakes != null)
        {
            Call("OnDestroy");
        }

        Object.DestroyImmediate(field);
        Object.DestroyImmediate(wall);
        Object.DestroyImmediate(mist);
        Assert.IsNull(MistWakes.Current, "the field forgets itself when it goes");
    }

    [Test]
    public void ABodyPartsTheMist_AWallStandsInItsWay_AndTheRestIsLeftAlone()
    {
        Vector3 focus = field.transform.position;
        Vector3 body = focus + new Vector3(-6f, 0f, 0f);
        for (int frame = 0; frame < 3; frame++)
        {
            MistWakes.Stamp(body, 1.2f, new Vector3(3f, 0f, 0f), 1f);
            Call("LateUpdate");
        }

        float[] parted = Read("current", out int size);
        float[] solid = Read("solid", out _);
        Vector2 min = (Vector2)typeof(MistWakes).GetField("min", Private).GetValue(wakes);
        float squareSize = MistWakeSettings.Current.Size;
        int Index(Vector3 at) =>
            Mathf.Clamp(Mathf.FloorToInt((at.z - min.y) / squareSize * size), 0, size - 1) * size
            + Mathf.Clamp(Mathf.FloorToInt((at.x - min.x) / squareSize * size), 0, size - 1);

        Assert.Greater(parted[Index(body)], 0.5f, "the mist is parted where the body stands");
        Assert.Greater(solid[Index(wall.transform.position)], 0.5f, "the wall is solid in the field");
        Assert.Greater(parted[Index(wall.transform.position)], MistWakeSettings.Current.ObstacleParting * 0.8f, "no mist inside the wall");
        Assert.Less(solid[Index(body)], 0.01f, "the body is not taken for an obstacle");

        // The canary: most of the square is left alone.
        int touched = 0;
        for (int i = 0; i < parted.Length; i++)
        {
            touched += parted[i] > 0.1f ? 1 : 0;
        }

        Assert.Less(touched, parted.Length / 20, $"{touched} of {parted.Length} texels parted: the field must leave the open mist alone");
    }

    [Test]
    public void WithNoMistShowing_TheFieldRests()
    {
        mist.SetActive(false);
        MistWakes.Stamp(field.transform.position, 1.2f, Vector3.zero, 1f);
        Call("LateUpdate");
        Assert.AreEqual(0f, Shader.GetGlobalVector("_MistWakeParams").w, "the march is told there is no wake to read");
        mist.SetActive(true);
        Call("LateUpdate");
        Assert.AreEqual(1f, Shader.GetGlobalVector("_MistWakeParams").w, "and reads it again when mist comes back");
    }

    [Test]
    public void ABlowThrowsTheMistTheWayItGoes_AndASpinThrowsItRound()
    {
        Vector3 origin = field.transform.position;
        var gusts = (IList)typeof(MistWakes).GetField("gusts", Private).GetValue(wakes);
        MistWakes.Strike(origin, Vector3.right, 3f, 100f);
        Assert.AreEqual(3, gusts.Count, "a blow throws the mist across its arc");
        foreach (object gust in gusts)
        {
            Vector3 at = Field<Vector3>(gust, "At");
            Vector3 velocity = Field<Vector3>(gust, "Velocity");
            Assert.Greater(at.x, origin.x + 1f, "ahead of the one striking");
            Assert.Greater(velocity.x, 0f, "thrown the way the blow goes");
            Assert.Less(Mathf.Abs(at.x - origin.x), 3f + 0.01f, "within the blow's reach");
        }

        gusts.Clear();
        MistWakes.Strike(origin, Vector3.right, 3f, 360f);
        Assert.AreEqual(6, gusts.Count, "a spin throws it all round");
        float ahead = 0f, behind = 0f;
        foreach (object gust in gusts)
        {
            float x = Field<Vector3>(gust, "At").x - origin.x;
            ahead = Mathf.Max(ahead, x);
            behind = Mathf.Min(behind, x);
            Assert.Greater(Vector3.Dot(Field<Vector3>(gust, "Velocity"), Field<Vector3>(gust, "At") - origin), 0f, "outward");
        }

        Assert.Greater(ahead, 1f);
        Assert.Less(behind, -1f, "behind as well as ahead");
    }

    private void Call(string method) => typeof(MistWakes).GetMethod(method, Private).Invoke(wakes, null);

    private static T Field<T>(object boxed, string name) => (T)boxed.GetType().GetField(name).GetValue(boxed);

    private float[] Read(string texture, out int size)
    {
        var target = (RenderTexture)typeof(MistWakes).GetField(texture, Private).GetValue(wakes);
        size = target.width;
        var read = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false);
        RenderTexture active = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            read.Apply();
            Color[] pixels = read.GetPixels();
            var red = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                red[i] = pixels[i].r;
            }

            return red;
        }
        finally
        {
            RenderTexture.active = active;
            Object.DestroyImmediate(read);
        }
    }
}
