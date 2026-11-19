using UnityEngine;

/// <summary>
/// Keeps a rescue rumour's quest waypoint (RaidRumourDirector.PointTheWay) on the middle of the map's search area while the site
/// is still to be found, and on the site itself once it is: roughly where to look, then where they are.
/// </summary>
public sealed class RescueWaypointFollower : MonoBehaviour
{
    private RaidEventSite site;
    private QuestWaypoint waypoint;
    private string person;
    private bool found;

    public void Bind(RaidEventSite rumourSite, QuestWaypoint mark, string personName)
    {
        site = rumourSite;
        waypoint = mark;
        person = personName;
        Place();
    }

    private void Update()
    {
        if (site == null || waypoint == null)
        {
            return;
        }

        if (site.IsResolved)
        {
            // Once helped up, the traveler leads on (its own waypoint, if it has one): this mark is done.
            Destroy(gameObject);
            return;
        }

        if (!found)
        {
            Place();
        }
    }

    private void Place()
    {
        Vector3 from = Player.Current != null ? Player.Current.transform.position : site.transform.position;
        found = site.IsFoundFrom(from);
        transform.position = site.MarkFrom(from);
        if (found)
        {
            waypoint.Configure(waypoint.Quest, waypoint.Objective, person);
        }
    }
}
