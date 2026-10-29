using System;

/// <summary>
/// One part of what someone has to say, gathered by their <see cref="Talkable"/>: a quest giver's offer, a companion's
/// camp questions and orders, a resident's jobs. Each part adds its answers to the page; the first part (in component
/// order) with a line sets <see cref="MenuPage.Body"/>, so a part only sets it while it is still empty. After an answer
/// is said, <c>afterAnswer</c> opens the conversation again.
/// </summary>
public interface IConversationTopics
{
    void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer);
}
