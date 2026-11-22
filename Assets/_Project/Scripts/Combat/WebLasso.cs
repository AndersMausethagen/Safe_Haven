using UnityEngine;

/// <summary>
/// A creature's lasso on the player (the Giant Black Widow's web, AttackDefinition › Lasso Seconds): a hit that lands holds the
/// player on a line and reels them in toward its bite, controls held but for Dodge, which they hammer to tear free (as from the
/// Nøkken, whose struggle bar FishingHud shows for this too). It lets go when the holder falls, at its time, or when the player
/// does. Only the local player is lassoed: a blow on a friend is their game's to judge. Numbers in GrabSettings.
/// </summary>
public sealed class WebLasso : MonoBehaviour
{
    private Combatant holder;
    private Damageable holderHealth;
    private SpiderLegs holderLegs;
    private Transform player;
    private Damageable playerHealth;
    private LineRenderer line;
    private float until;
    private float presses;
    private bool over;

    /// <summary>The lasso holding the local player, or null.</summary>
    public static WebLasso Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>0..1: how close the player is to tearing free.</summary>
    public float Struggle => Mathf.Clamp01(presses / Mathf.Max(1, GrabSettings.Current.PressesToEscape));

    public float SecondsLeft => Mathf.Max(0f, until - Time.time);

    /// <summary>After a blow lands: a lasso attack that struck the local player holds them, unless something else already does.</summary>
    public static void AfterHit(AttackDefinition attack, IDamageable victim, Combatant source, in DamageResult result)
    {
        if (attack == null || attack.LassoSeconds <= 0f || source == null || !result.Landed || result.Killed || result.Parried || result.Blocked
            || !(victim is Component body) || body == null || Player.Current == null || body.gameObject != Player.Current.gameObject
            || Current != null || NokkenEncounter.Current != null)
        {
            return;
        }

        var host = new GameObject("Web Lasso");
        host.AddComponent<WebLasso>().Begin(source, Player.Current.transform, attack.LassoSeconds);
    }

    private void Begin(Combatant source, Transform target, float holdSeconds)
    {
        Current = this;
        holder = source;
        holderHealth = source.GetComponent<Damageable>();
        holderLegs = source.GetComponent<SpiderLegs>();
        player = target;
        playerHealth = target.GetComponent<Damageable>();
        until = Time.time + holdSeconds;
        GrabSettings settings = GrabSettings.Current;
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.widthMultiplier = settings.LineWidth;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (settings.LineMaterial != null)
        {
            line.sharedMaterial = settings.LineMaterial;
        }

        DrawLine();
        PlayerControl.Set(player.gameObject, this, true, PlayerControl.Kept.Dodge);
        AudioManager.Play(settings.GrabSound, player.position);
        CameraShake.Add(0.25f);
        GameMessages.Post(GameInput.Keys($"The {Name}'s web has you! Hammer {{key:Dodge}} to tear free!"), MessageKind.Warning);
    }

    private string Name
    {
        get
        {
            MonsterAI monster = holder != null ? holder.GetComponent<MonsterAI>() : null;
            return monster != null && monster.Definition != null ? monster.Definition.DisplayName : "creature";
        }
    }

    private void Update()
    {
        if (over)
        {
            return;
        }

        if (player == null || playerHealth == null || !playerHealth.IsAlive)
        {
            End(false, null);
            return;
        }

        if (holder == null || !holder.gameObject.activeInHierarchy || (holderHealth != null && !holderHealth.IsAlive))
        {
            End(true, "The web goes slack.");
            return;
        }

        GrabSettings settings = GrabSettings.Current;
        AudioManager.KeepCombat();
        CameraShake.AtLeast(settings.HoldShake);
        bool pressed = GameInput.Dodge.WasPressedThisFrame();
        presses = Mathf.Max(0f, presses + (pressed ? 1f : 0f) - settings.PressesFadePerSecond * Time.deltaTime);
        if (pressed)
        {
            CameraShake.Add(0.05f);
        }

        // Reeled in toward its bite, along the ground.
        Vector3 toward = holder.transform.position - player.position;
        toward.y = 0f;
        float size = Mathf.Max(0.3f, holder.transform.lossyScale.y);
        if (toward.magnitude > settings.HoldDistance * size && player.TryGetComponent(out CharacterController body) && body.enabled)
        {
            body.Move(toward.normalized * settings.ReelSpeed * Time.deltaTime + Vector3.down * 4f * Time.deltaTime);
        }

        DrawLine();
        if (Struggle >= 1f)
        {
            End(true, "You tear free of the web!");
        }
        else if (Time.time >= until)
        {
            End(true, "The web gives way.");
        }
    }

    private void DrawLine()
    {
        if (line == null || holder == null || player == null)
        {
            return;
        }

        Transform from = holderLegs != null && holderLegs.Body != null ? holderLegs.Body : holder.transform;
        line.SetPosition(0, from.position + (holderLegs != null ? Vector3.zero : Vector3.up));
        line.SetPosition(1, player.position + Vector3.up * 1.1f);
    }

    private void End(bool freed, string message)
    {
        over = true;
        if (freed && player != null)
        {
            PlayerControl.Set(player.gameObject, this, false, PlayerControl.Kept.Dodge);
            AudioManager.Play(GrabSettings.Current.SnapSound, player.position);
        }

        // A player it held to death keeps the controls their death took (HomeRevival, RaidDirector): giving them back would let a
        // dead body walk.
        if (!string.IsNullOrEmpty(message))
        {
            GameMessages.Post(message, Struggle >= 1f ? MessageKind.Gain : MessageKind.Info);
        }

        if (Current == this)
        {
            Current = null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Gone some other way (the scene unloading): no hold is left behind.
        if (!over && player != null)
        {
            PlayerControl.Set(player.gameObject, this, false, PlayerControl.Kept.Dodge);
        }

        if (Current == this)
        {
            Current = null;
        }
    }
}
