using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Dumps the player's animator layers, states and transitions (`unity command run_script --file
/// AgentScripts/ProbeAnimator.cs --entry ProbeAnimator.Dump --args '["Arms"]'`; empty layer name = all, states only).</summary>
public static class ProbeAnimator
{
    public static string Dump(string layerName)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animation/Controllers/PlayerAnimator.controller");
        var text = new StringBuilder();
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            bool detail = layer.name == layerName;
            text.AppendLine($"[{layer.name}] weight={layer.defaultWeight} mask={(layer.avatarMask ? layer.avatarMask.name : "-")} blend={layer.blendingMode} ik={layer.iKPass} default={layer.stateMachine.defaultState?.name}");
            if (!string.IsNullOrEmpty(layerName) && !detail)
            {
                continue;
            }

            foreach (ChildAnimatorState child in layer.stateMachine.states)
            {
                AnimatorState state = child.state;
                string motion = state.motion is BlendTree tree ? $"tree({tree.blendParameter},{tree.blendParameterY},{tree.children.Length})" : state.motion ? state.motion.name : "-";
                text.AppendLine($"  {state.name} motion={motion} speed={state.speed}{(state.speedParameterActive ? "*" + state.speedParameter : "")}");
                if (!detail)
                {
                    continue;
                }

                foreach (AnimatorStateTransition t in state.transitions)
                {
                    text.AppendLine($"    -> {(t.destinationState ? t.destinationState.name : "exit")} exit={t.hasExitTime}:{t.exitTime:F2} dur={t.duration:F2} " +
                                    string.Join(" & ", t.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}")));
                }
            }

            if (detail)
            {
                foreach (AnimatorStateTransition t in layer.stateMachine.anyStateTransitions)
                {
                    text.AppendLine($"  ANY -> {t.destinationState?.name} dur={t.duration:F2} " + string.Join(" & ", t.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}")));
                }
            }
        }

        text.AppendLine("params: " + string.Join(", ", controller.parameters.Select(p => p.name + ":" + p.type)));
        return text.ToString();
    }
}
