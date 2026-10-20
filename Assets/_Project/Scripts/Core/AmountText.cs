using System.Collections.Generic;
using System.Text;

/// <summary>
/// Writes amounts of things the way the player reads them everywhere: "3 Iron Ingot, 2 Bone". One place for this text,
/// so loot messages, gathering messages and quest rewards always agree. Extend it here (pluralisation, colours) rather
/// than formatting amounts in each system.
/// </summary>
public static class AmountText
{
    public static string Describe(IReadOnlyList<ItemAmount> amounts)
    {
        var builder = new StringBuilder();
        foreach (ItemAmount amount in amounts)
        {
            if (amount.Item != null)
            {
                Append(builder, amount.Amount, amount.Item.DisplayName);
            }
        }

        return builder.ToString();
    }

    public static string Describe(IReadOnlyList<ResourceAmount> amounts)
    {
        var builder = new StringBuilder();
        foreach (ResourceAmount amount in amounts)
        {
            if (amount.Resource != null)
            {
                Append(builder, amount.Amount, amount.Resource.DisplayName);
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, int amount, string name)
    {
        builder.Append(builder.Length > 0 ? ", " : "").Append(amount).Append(' ').Append(name);
    }
}
