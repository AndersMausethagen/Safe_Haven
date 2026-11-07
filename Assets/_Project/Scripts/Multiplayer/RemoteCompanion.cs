using System;
using TMPro;
using UnityEngine;

/// <summary>
/// A friend's raid companion as you see them: the resident's own model with their animations and weapon, made the same
/// way as the companion in the friend's game (<see cref="CompanionBody"/>), placed and animated from the friend's poses,
/// with their name over their head ("Sigrid · Down!" while down) and an "[E] Help up" that asks the friend's game to get
/// them up. Nothing here fights or can be hit: the companion lives in the friend's game. Plain C#, owned by
/// <see cref="NetworkPlayerAvatar"/>.
/// </summary>
public sealed class RemoteCompanion
{
    private const float NameHeight = 2.1f;

    private readonly Action<bool> helping;
    private readonly Action helped;
    private readonly float helpSeconds;
    private GameObject body;
    private AnimatorMirror mirror;
    private TextMeshPro nameTag;
    private FriendRevive revive;
    private string shownId = string.Empty;
    private string displayName = string.Empty;
    private Camera viewer;

    /// <param name="helping">Called as someone here starts (true) or stops (false) helping them up.</param>
    /// <param name="helped">Called when they are helped up here: the friend's game gets them up.</param>
    public RemoteCompanion(Action<bool> helping, Action helped, float helpSeconds)
    {
        this.helping = helping;
        this.helped = helped;
        this.helpSeconds = helpSeconds;
    }

    /// <summary>The body, or null while none is shown.</summary>
    public GameObject Body => body;

    /// <summary>Shows the companion <paramref name="state"/> names (made again if it is someone else), or nothing when none is out.</summary>
    public void Show(in CompanionLookState state, IContentLookup content)
    {
        if (!state.IsOut)
        {
            Hide();
            return;
        }

        string id = state.Resident.ToString();
        if (body == null || id != shownId)
        {
            Make(id, content);
        }

        if (body == null)
        {
            return;
        }

        if (!body.activeSelf)
        {
            body.SetActive(true);
        }

        UIStyle style = UIStyle.Current;
        string shown = state.Downed ? $"{displayName} · Down!" : displayName;
        if (nameTag.text != shown)
        {
            nameTag.text = shown;
            nameTag.color = state.Downed ? style.Danger : style.Text;
        }

        revive.Show(state.Downed, displayName);
    }

    /// <summary>Where they stand and how they move now, from the friend's poses.</summary>
    public void Place(Vector3 position, float yaw, in AnimatorFrame animation, float blend)
    {
        if (body == null || !body.activeSelf)
        {
            return;
        }

        body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        mirror?.Apply(animation, blend);
        if (viewer == null || !viewer.isActiveAndEnabled)
        {
            viewer = Camera.main;
        }

        if (viewer != null)
        {
            nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - viewer.transform.position, Vector3.up);
        }
    }

    public void Hide()
    {
        if (body != null && body.activeSelf)
        {
            body.SetActive(false);
        }
    }

    public void Destroy()
    {
        if (body != null)
        {
            UnityEngine.Object.Destroy(body);
        }

        body = null;
    }

    private void Make(string id, IContentLookup content)
    {
        Destroy();
        shownId = id;
        ResidentDefinition resident = content != null ? content.FindResident(id) : null;
        if (resident == null || resident.Model == null)
        {
            return;
        }

        displayName = resident.DisplayName;
        body = new GameObject($"{displayName} (a friend's companion)");
        GameObject model = UnityEngine.Object.Instantiate(resident.Model, body.transform, false);
        Animator animator = model.GetComponentInChildren<Animator>();
        var look = new CompanionBody(animator, resident.RaidCompanion);
        look.Arm(resident.RaidCompanion);
        if (animator != null)
        {
            // Placed from the friend's poses, not by root motion.
            animator.applyRootMotion = false;
            mirror = new AnimatorMirror(animator);
        }

        nameTag = RemotePlayerBody.NameTag(body.transform, NameHeight);
        nameTag.text = displayName;
        revive = FriendRevive.Create(body.transform, displayName, helpSeconds, helping, helped);
    }
}
