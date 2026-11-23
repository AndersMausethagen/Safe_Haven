/// <summary>
/// What who a person is adds to their work speed (Docs/PeopleResearch_2026-11-23.md): the joy of a skill they love (ResidentTalents),
/// their traits and marks (ResidentTraits), their spirits (Low is slower: ResidentSpirits), friends working near them (PeopleStandings)
/// and an inspired day's fever (Inspirations). One share, added to their home's and meal's by HomeLife.WorkBonus, which every kind of
/// work reads (stations, scaffolds, rings). Asked every frame: nothing here allocates.
/// </summary>
public static class PeopleWork
{
    /// <summary>Their work speed share at work with <paramref name="skill"/> (0.1 = 10% quicker).</summary>
    public static float Bonus(ResidentRecord record, ProficiencyDefinition skill)
    {
        if (record == null)
        {
            return 0f;
        }

        return ResidentTalents.Joy(record, skill) + ResidentTraits.WorkShare(record, skill) + ResidentSpirits.WorkShare(record)
               + PeopleStandings.WorkShare(record) + Inspirations.WorkShare(record);
    }
}
