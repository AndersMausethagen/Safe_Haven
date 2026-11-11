using System;
using UnityEngine;

/// <summary>
/// Plays emotes on the Animator's "Emote" layer (built by EmoteLayerBuilder): one-shots that fade out when done, and held
/// poses (sitting, sleeping) that last until <see cref="Stop"/> (playing their ending) or until the character starts moving
/// (no ending: the layer fades out under the steps). The layer only has
/// weight while an emote plays, so it never covers the other layers otherwise.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[AddComponentMenu("Safe Haven/Characters/Character Emotes")]
public sealed class CharacterEmotes : MonoBehaviour
{
    public const string LayerName = "Emote";
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    [SerializeField, Min(0.01f)] private float fade = 0.25f;
    [Tooltip("Moving faster than this ends a held emote.")]
    [SerializeField, Min(0f)] private float moveToStop = 0.3f;

    private Animator animator;
    private RuntimeAnimatorController cachedController;
    private int layer = -1;
    private bool hasSpeed;
    private float weight;
    private EmoteDefinition current;
    private Phase phase;
    private float started;

    /// <summary>Raised when an emote has ended, by finishing, by being stopped or by being replaced.</summary>
    public event Action<EmoteDefinition> Ended;

    public EmoteDefinition Current => phase == Phase.None ? null : current;
    public bool IsPlaying => phase != Phase.None;
    public bool IsHolding => phase != Phase.None && current != null && current.IsHeld && phase != Phase.Ending;

    /// <summary>True while the character walks or runs (by the Animator's Speed, when it has one).</summary>
    public bool IsMoving => hasSpeed && animator != null && animator.GetFloat(SpeedHash) > moveToStop;

    private enum Phase
    {
        None,
        Playing,
        Ending,
    }

    private void Awake() => Cache();

    private void OnDisable()
    {
        if (phase != Phase.None)
        {
            Finish();
        }

        weight = 0f;
        if (animator != null && layer >= 0)
        {
            animator.SetLayerWeight(layer, 0f);
        }
    }

    /// <summary>Plays the emote, replacing any other. False when this character's Animator has no such state.</summary>
    public bool Play(EmoteDefinition emote)
    {
        Cache();
        if (emote == null || layer < 0)
        {
            return false;
        }

        string state = emote.HasBegin ? emote.BeginState : emote.StateName;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(layer, hash))
        {
            return false;
        }

        if (phase != Phase.None)
        {
            Finish();
        }

        current = emote;
        phase = Phase.Playing;
        started = Time.time;
        animator.CrossFadeInFixedTime(hash, fade, layer);
        return true;
    }

    /// <summary>Ends the emote playing: a held pose plays its ending first.</summary>
    public void Stop()
    {
        if (phase != Phase.Playing || current == null)
        {
            return;
        }

        int end = Animator.StringToHash(current.EndState);
        if (current.HasEnd && animator.HasState(layer, end))
        {
            phase = Phase.Ending;
            started = Time.time;
            animator.CrossFadeInFixedTime(end, fade, layer);
        }
        else
        {
            Finish();
        }
    }

    private void Update()
    {
        if (animator.runtimeAnimatorController != cachedController)
        {
            Cache();
        }

        if (layer < 0)
        {
            return;
        }

        // Walking off ends any emote at once and the layer fades out under the steps. Playing a held pose's ending (getting
        // up from the ground, 2 s) or a one-shot on, at full weight, slid the character along in it.
        if (phase != Phase.None && IsMoving && (phase == Phase.Ending || Time.time - started > fade))
        {
            Finish();
        }
        else if (phase != Phase.None && Time.time - started > fade)
        {
            UpdatePhase();
        }

        float target = phase == Phase.None ? 0f : 1f;
        if (!Mathf.Approximately(weight, target))
        {
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / fade);
            animator.SetLayerWeight(layer, weight);
        }
    }

    private void UpdatePhase()
    {
        if (animator.IsInTransition(layer))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
        bool oneShot = phase == Phase.Playing && !current.IsHeld && state.IsName(current.StateName);
        bool ending = phase == Phase.Ending && state.IsName(current.EndState);
        if ((oneShot || ending) && state.normalizedTime >= 1f - fade / Mathf.Max(state.length, fade))
        {
            Finish();
        }
        else if (!state.IsName(current.StateName) && !state.IsName(current.BeginState) && !state.IsName(current.EndState))
        {
            // Something else took the layer (the controller was replaced): this emote is over.
            Finish();
        }
    }

    private void Finish()
    {
        EmoteDefinition ended = current;
        phase = Phase.None;
        current = null;
        Ended?.Invoke(ended);
    }

    private void Cache()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        cachedController = animator.runtimeAnimatorController;
        layer = cachedController != null ? animator.GetLayerIndex(LayerName) : -1;
        weight = layer >= 0 ? animator.GetLayerWeight(layer) : 0f;
        hasSpeed = false;
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeed = true;
            }
        }
    }
}
