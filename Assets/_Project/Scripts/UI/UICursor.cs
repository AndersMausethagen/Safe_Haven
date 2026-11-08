using UnityEngine;

/// <summary>
/// The Hearthline pointer (UIStyle > Cursor): a dark arrowhead with an amber edge, set once as the game starts. The
/// hardware cursor, so it costs nothing and never lags. The tip is the image's top-left corner.
/// </summary>
public static class UICursor
{
    private static readonly Vector2 Tip = new Vector2(2f, 2f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Apply()
    {
        Texture2D cursor = UIStyle.Current.Cursor;
        if (cursor != null)
        {
            Cursor.SetCursor(cursor, Tip, CursorMode.Auto);
        }
    }
}
