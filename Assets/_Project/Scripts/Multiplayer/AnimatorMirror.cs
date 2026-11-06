using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One moment of an Animator: the state each layer is in (or heading for) with its time and weight, and the values of its
/// float, int and bool parameters. Triggers are left out: the state they lead to is sent instead.
/// </summary>
public struct AnimatorFrame : INetworkSerializable
{
    public int[] States;
    public float[] Times;
    public float[] Weights;
    /// <summary>Float and int parameters, in the controller's order (ints as floats).</summary>
    public float[] Values;
    /// <summary>Bool parameters, one bit each in the controller's order.</summary>
    public ulong Flags;

    public int LayerCount => States?.Length ?? 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int layers = States?.Length ?? 0;
        int values = Values?.Length ?? 0;
        serializer.SerializeValue(ref layers);
        serializer.SerializeValue(ref values);
        if (serializer.IsReader)
        {
            States = new int[layers];
            Times = new float[layers];
            Weights = new float[layers];
            Values = new float[values];
        }

        for (int i = 0; i < layers; i++)
        {
            serializer.SerializeValue(ref States[i]);
            serializer.SerializeValue(ref Times[i]);
            serializer.SerializeValue(ref Weights[i]);
        }

        for (int i = 0; i < values; i++)
        {
            serializer.SerializeValue(ref Values[i]);
        }

        serializer.SerializeValue(ref Flags);
    }
}

/// <summary>
/// Copies what one Animator is doing onto another with the same controller: the player's own Animator is read on their
/// machine, and a friend's body is made to match on everyone else's. Parameters are set so the controller's own
/// transitions and blend trees behave as at home; a layer in a different state is blended into the sent one, which covers
/// what triggers and code-driven cross-fades (attacks, interactions, emotes) start. Plain C#.
/// </summary>
public sealed class AnimatorMirror
{
    private readonly Animator animator;
    private AnimatorControllerParameter[] parameters = Array.Empty<AnimatorControllerParameter>();
    private RuntimeAnimatorController readFrom;
    private int valueCount;

    public AnimatorMirror(Animator animator)
    {
        this.animator = animator;
    }

    /// <summary>Reads the Animator into <paramref name="frame"/>, reusing its arrays.</summary>
    public void Capture(ref AnimatorFrame frame)
    {
        if (!Ready())
        {
            frame.States = Array.Empty<int>();
            return;
        }

        int layers = animator.layerCount;
        Ensure(ref frame.States, layers);
        Ensure(ref frame.Times, layers);
        Ensure(ref frame.Weights, layers);
        Ensure(ref frame.Values, valueCount);
        for (int layer = 0; layer < layers; layer++)
        {
            AnimatorStateInfo state = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
            frame.States[layer] = state.fullPathHash;
            frame.Times[layer] = state.normalizedTime;
            frame.Weights[layer] = layer == 0 ? 1f : animator.GetLayerWeight(layer);
        }

        int value = 0;
        int bit = 0;
        ulong flags = 0;
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    frame.Values[value++] = animator.GetFloat(parameter.nameHash);
                    break;
                case AnimatorControllerParameterType.Int:
                    frame.Values[value++] = animator.GetInteger(parameter.nameHash);
                    break;
                case AnimatorControllerParameterType.Bool:
                    if (bit < 64 && animator.GetBool(parameter.nameHash))
                    {
                        flags |= 1UL << bit;
                    }

                    bit++;
                    break;
            }
        }

        frame.Flags = flags;
    }

    /// <summary>Makes the Animator match <paramref name="frame"/>, blending into changed states over <paramref name="blend"/> seconds.</summary>
    public void Apply(in AnimatorFrame frame, float blend)
    {
        if (!Ready() || frame.Values == null || frame.Values.Length != valueCount)
        {
            return;
        }

        int value = 0;
        int bit = 0;
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(parameter.nameHash, frame.Values[value++]);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(parameter.nameHash, Mathf.RoundToInt(frame.Values[value++]));
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(parameter.nameHash, bit < 64 && (frame.Flags & (1UL << bit)) != 0);
                    bit++;
                    break;
            }
        }

        int layers = Mathf.Min(frame.LayerCount, animator.layerCount);
        for (int layer = 0; layer < layers; layer++)
        {
            if (layer > 0)
            {
                animator.SetLayerWeight(layer, frame.Weights[layer]);
            }

            int wanted = frame.States[layer];
            if (wanted == 0 || wanted == animator.GetCurrentAnimatorStateInfo(layer).fullPathHash ||
                (animator.IsInTransition(layer) && wanted == animator.GetNextAnimatorStateInfo(layer).fullPathHash))
            {
                continue;
            }

            if (animator.HasState(layer, wanted))
            {
                // Looping states repeat past 1: start at the same point in the loop. One-shots start where the owner is.
                float time = frame.Times[layer];
                animator.CrossFadeInFixedTime(wanted, blend, layer, 0f, time > 1f ? Mathf.Repeat(time, 1f) : Mathf.Max(0f, time));
            }
        }
    }

    // The parameters are read once per controller (a woman's body swaps in an override with the same parameters).
    private bool Ready()
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
        {
            return false;
        }

        if (readFrom != animator.runtimeAnimatorController)
        {
            readFrom = animator.runtimeAnimatorController;
            parameters = animator.parameters;
            valueCount = 0;
            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float || parameter.type == AnimatorControllerParameterType.Int)
                {
                    valueCount++;
                }
            }
        }

        return true;
    }

    private static void Ensure<T>(ref T[] array, int length)
    {
        if (array == null || array.Length != length)
        {
            array = new T[length];
        }
    }
}
