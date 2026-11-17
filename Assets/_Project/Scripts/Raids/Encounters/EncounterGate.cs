using UnityEngine;

/// <summary>
/// The gate of mist in a boss encounter's arena (EncounterArena): using it shows what lies beyond, what the gate asks, and what is at
/// stake (what a failure takes, and the wear on the gear), then the player steps through or turns back. Once through, the mist is
/// sealed behind them.
/// <para>
/// Where it belongs: on the gate's object, beside an Interactable (a short use, not deactivated on completion), referenced by the
/// scene's EncounterArena.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[OnAFriendsCopy(CopyRule.Same, "each player steps through their own game's gate")]
public sealed class EncounterGate : MonoBehaviour, IInteractionCondition, IInteractionVerb
{
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private Interactable interactable;

    public string Verb => EncounterArena.Current != null && EncounterArena.Current.Definition != null
        ? EncounterArena.Current.Definition.GateVerb : "Step through the mist";

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable() => interactable.Completed.AddListener(OnUsed);

    private void OnDisable() => interactable.Completed.RemoveListener(OnUsed);

    public bool CanInteract(GameObject user, out string reason)
    {
        EncounterArena arena = EncounterArena.Current;
        reason = arena == null ? "The mist doesn't part." : arena.IsSealed ? "The mist is sealed behind you." : null;
        return reason == null;
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (user != null && ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => Page(user), user, transform, menuRange);
        }
    }

    private MenuPage Page(GameObject user)
    {
        EncounterArena arena = EncounterArena.Current;
        BossEncounterDefinition encounter = arena != null ? arena.Definition : null;
        var page = new MenuPage { Title = "The Mist" };
        if (arena == null || arena.IsSealed)
        {
            page.Body = "The mist is sealed.";
            return page;
        }

        string bossName = arena.BossName;
        var body = new System.Text.StringBuilder();
        body.Append(encounter != null && !string.IsNullOrWhiteSpace(encounter.GateText) ? encounter.GateText : "Beyond the mist there is no way back but through it.");
        body.Append("\n\n").Append($"Beyond it: {bossName}. The only ways out are bringing it down or falling.");
        if (encounter != null)
        {
            body.Append($"\nIt enrages after {RaidStats.FormatTime(encounter.EnrageSeconds)}.");
        }

        RaidDirector director = RaidDirector.Current;
        if (director != null)
        {
            RaidRisk risk = RaidRisk.Of(PlayerSnapshot.Capture(user), director.Session.Loadout);
            float wear = encounter != null ? encounter.WearOnFailure : DurabilitySettings.Current.LossOnFailedRaid;
            body.Append("\n\nIf you fall: ")
                .Append(risk.IsEmpty ? "you carry nothing that would be lost" : $"{risk.Items} items ({risk.Value} value) and {risk.Resources} materials are lost")
                .Append($", and your gear wears {Mathf.RoundToInt(wear * 100f)}%.");
        }

        string cost = encounter != null && encounter.HasGateCost ? Price(encounter) : "";
        if (cost.Length > 0)
        {
            body.Append("\nThe gate asks: ").Append(cost).Append('.');
        }

        page.Body = body.ToString();
        bool canPay = encounter == null || !encounter.HasGateCost || MaterialPool.For(user).CanAfford(encounter.GateResources, encounter.GateItems, out _);
        page.Add(new MenuAction(Verb, canPay ? null : "You don't have what the gate asks.", canPay, () =>
        {
            if (arena.Enter(user, out string problem))
            {
                ActionMenuWindow.Current.Close();
            }
            else if (!string.IsNullOrEmpty(problem))
            {
                GameMessages.Post(problem, MessageKind.Warning);
            }
        }));
        page.Add(new MenuAction("Not yet", null, true, () => ActionMenuWindow.Current.Close()));
        return page;
    }

    private static string Price(BossEncounterDefinition encounter)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (ResourceAmount resource in encounter.GateResources)
        {
            if (resource.Resource != null && resource.Amount > 0)
            {
                parts.Add($"{resource.Amount}× {resource.Resource.DisplayName}");
            }
        }

        foreach (ItemAmount item in encounter.GateItems)
        {
            if (item.Item != null && item.Amount > 0)
            {
                parts.Add($"{item.Amount}× {item.Item.DisplayName}");
            }
        }

        return string.Join(", ", parts);
    }
}
