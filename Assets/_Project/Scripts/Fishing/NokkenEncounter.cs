using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Nøkken: sometimes, when a bite in still water is struck, it is not a fish. A dark shape with pale eyes rises from
/// the water beside the player with a scream and a splash, the camera is pulled in close, and it grips the player and
/// drags them towards the deep, hurting them all the while. Hammering Space (or the gamepad's south button) breaks free;
/// if it holds on until the time is up, it drags the player under and lets go only after taking a third of their health. The
/// first meeting is longer and closer, and raises FishingSettings' Met Nøkken Quest Event. Everything is put back when it
/// ends (input, camera, the player out of the water). Made by PlayerFishing; numbers and sounds in FishingSettings.
/// </summary>
public sealed class NokkenEncounter : MonoBehaviour
{
    private const float RiseSeconds = 0.7f;
    private const float PullSpeed = 0.35f;
    private const float PressesFadePerSecond = 0.8f;

    private PlayerFishing fishing;
    private Transform figure;
    private Vector3 surfaceAt;
    private float started;
    private float presses;
    private float nextHurt;
    private bool first;
    private bool over;
    private Action done;

    public static NokkenEncounter Current { get; private set; }

    /// <summary>0..1: how close the player is to breaking free.</summary>
    public float Struggle => Mathf.Clamp01(presses / Mathf.Max(1, FishingSettings.Current.NokkenPresses));

    public float SecondsLeft => Mathf.Max(0f, FishingSettings.Current.NokkenSeconds + (first ? 2f : 0f) - (Time.time - started));

    public bool First => first;

    /// <summary>Starts the encounter at the water where the float lay; <paramref name="ended"/> runs when it is over.</summary>
    public static NokkenEncounter Begin(PlayerFishing fishing, Vector3 floatAt, float waterHeight, Action ended)
    {
        var host = new GameObject("Nøkken");
        var encounter = host.AddComponent<NokkenEncounter>();
        encounter.fishing = fishing;
        encounter.done = ended;
        encounter.Rise(floatAt, waterHeight);
        return encounter;
    }

    private void Rise(Vector3 floatAt, float waterHeight)
    {
        Current = this;
        FishingSettings settings = FishingSettings.Current;
        GameEventDefinition met = settings.MetNokken;
        first = met == null || !GameSession.Quests.HasHappened(met);
        started = Time.time;
        Transform player = fishing.transform;
        Vector3 toWater = new Vector3(floatAt.x - player.position.x, 0f, floatAt.z - player.position.z);
        surfaceAt = WaterEdge(player.position, floatAt, toWater.normalized, waterHeight);
        figure = Figure(settings.NokkenModel).transform;
        figure.SetParent(transform, false);
        figure.position = surfaceAt + Vector3.down * 2.5f;
        figure.rotation = Quaternion.LookRotation(-toWater.normalized);

        AudioManager.Play(settings.NokkenScream, surfaceAt);
        AudioManager.Play(settings.NokkenSplash, surfaceAt);
        if (first)
        {
            AudioManager.Play(settings.NokkenScream, player.position);
        }

        CameraShake.Add(first ? 0.9f : 0.6f);
        PlayerCameraController camera = PlayerCameraController.Current;
        if (camera != null)
        {
            camera.BeginDialogue(figure, first ? 0.3f : 0.42f);
        }

        SetInput(false);
        GameMessages.Post(first ? "Nøkken! The water spirit has you. Hammer SPACE to break free!" : "The Nøkken! Hammer SPACE!", MessageKind.Warning);
        nextHurt = Time.time + 1f;
    }

    private void Update()
    {
        if (over)
        {
            return;
        }

        float risen = Mathf.Clamp01((Time.time - started) / RiseSeconds);
        figure.position = Vector3.Lerp(surfaceAt + Vector3.down * 2.5f, surfaceAt + Vector3.down * 0.4f, 1f - (1f - risen) * (1f - risen));

        Keyboard keys = Keyboard.current;
        bool pressed = keys != null && keys.spaceKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        presses = Mathf.Max(0f, presses + (pressed ? 1f : 0f) - PressesFadePerSecond * Time.deltaTime);
        if (pressed)
        {
            CameraShake.Add(0.06f);
        }

        // Dragged towards the deep.
        Transform player = fishing.transform;
        Vector3 pull = new Vector3(surfaceAt.x - player.position.x, 0f, surfaceAt.z - player.position.z);
        if (pull.magnitude > 0.6f && player.TryGetComponent(out CharacterController body))
        {
            body.Move(pull.normalized * PullSpeed * Time.deltaTime);
        }

        Damageable health = player.GetComponent<Damageable>();
        if (Time.time >= nextHurt)
        {
            nextHurt = Time.time + 1f;
            Hurt(health, FishingSettings.Current.NokkenDamagePerSecond);
        }

        if (health != null && !health.IsAlive)
        {
            End(false, true);
        }
        else if (Struggle >= 1f)
        {
            End(true, false);
        }
        else if (SecondsLeft <= 0f)
        {
            End(false, false);
        }
    }

    private void End(bool brokeFree, bool died)
    {
        over = true;
        FishingSettings settings = FishingSettings.Current;
        Transform player = fishing.transform;
        if (!brokeFree && !died)
        {
            Damageable health = player.GetComponent<Damageable>();
            Hurt(health, health != null ? health.MaxHealth * settings.NokkenWinsDamage : 0f);
        }

        // Thrown back onto the bank.
        Vector3 away = new Vector3(player.position.x - surfaceAt.x, 0f, player.position.z - surfaceAt.z).normalized;
        if (player.TryGetComponent(out CharacterController body))
        {
            body.Move(away * 2.2f);
        }

        AudioManager.Play(settings.NokkenSplash, surfaceAt);
        PlayerCameraController camera = PlayerCameraController.Current;
        if (camera != null)
        {
            camera.EndDialogue(figure);
        }

        SetInput(true);
        if (!died)
        {
            GameMessages.Post(brokeFree ? "You tear free. The Nøkken sinks back into the black water." :
                "The Nøkken drags you under... you claw your way back to the bank, coughing.", brokeFree ? MessageKind.Gain : MessageKind.Warning);
        }

        if (settings.MetNokken != null)
        {
            GameEvents.Raise(settings.MetNokken, player.gameObject);
        }

        if (brokeFree)
        {
            ExperienceTracker.Grant(first ? 80 : 30);
        }

        done?.Invoke();
        if (Current == this)
        {
            Current = null;
        }

        Destroy(gameObject, 0.1f);
    }

    // Just inside the water's edge between the player and the float, so it rises out of the water.
    private static Vector3 WaterEdge(Vector3 from, Vector3 floatAt, Vector3 direction, float waterHeight)
    {
        float distance = new Vector2(floatAt.x - from.x, floatAt.z - from.z).magnitude;
        for (float step = 0.5f; step < distance; step += 0.25f)
        {
            Vector3 probe = from + direction * step;
            if (WaterSurface.At(new Vector3(probe.x, waterHeight + 0.5f, probe.z), 1f, out _) != null)
            {
                Vector3 inside = from + direction * Mathf.Min(distance, step + 0.9f);
                return new Vector3(inside.x, waterHeight, inside.z);
            }
        }

        return new Vector3(floatAt.x, waterHeight, floatAt.z);
    }

    private void Hurt(Damageable health, float amount)
    {
        if (health == null || amount <= 0f || !health.IsAlive)
        {
            return;
        }

        Vector3 at = health.transform.position + Vector3.up;
        health.ApplyDamage(new DamageInfo(null, null, amount, DamageType.Physical, false, at, at - surfaceAt));
    }

    private void SetInput(bool on)
    {
        if (fishing.TryGetComponent(out PlayerInputHandler input))
        {
            input.enabled = on;
        }
    }

    // The model (stripped of anything that would make it act), darkened, with two pale eyes.
    private static GameObject Figure(GameObject model)
    {
        GameObject figure = model != null ? Instantiate(model) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        foreach (MonoBehaviour behaviour in figure.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        foreach (Collider collider in figure.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        if (figure.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
        {
            agent.enabled = false;
        }

        // Wet, black-green skin whatever the model's own shader.
        var skin = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        skin.SetColor("_BaseColor", new Color(0.03f, 0.06f, 0.06f));
        skin.SetFloat("_Smoothness", 0.85f);
        Bounds bounds = new Bounds(figure.transform.position, Vector3.zero);
        foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>(true))
        {
            bounds.Encapsulate(renderer.bounds);
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = skin;
            renderer.sharedMaterials = materials;
        }

        float scale = 2.6f / Mathf.Max(0.5f, bounds.size.y);
        figure.transform.localScale *= scale;
        float eyes = (bounds.max.y - figure.transform.position.y) * scale - 0.35f;
        foreach (float side in new[] { -0.09f, 0.09f })
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(figure.transform, true);
            eye.transform.localPosition = new Vector3(side, eyes / scale, 0.18f / scale);
            eye.transform.localScale = Vector3.one * 0.055f / scale;
            var glow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glow.color = new Color(0.75f, 1f, 0.9f);
            eye.GetComponent<Renderer>().material = glow;
        }

        var light = new GameObject("Eyes Light").AddComponent<Light>();
        light.transform.SetParent(figure.transform, false);
        light.transform.localPosition = new Vector3(0f, eyes / scale, 0.4f / scale);
        light.type = LightType.Point;
        light.color = new Color(0.5f, 0.9f, 0.85f);
        light.intensity = 2.5f;
        light.range = 4f;
        return figure;
    }
}
