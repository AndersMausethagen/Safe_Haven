using System.Collections.Generic;
using UnityEngine;
using static CodeUI;

/// <summary>
/// The left column, as pictures (the user, 2026-11-21: "Instead of the text ... it should be artwork of the different events, with
/// its name underneath ... When the player hovers over the picture, the text can come up"). Chips for this run's hour, map and sky;
/// a card for each thing out there (a Lost One walking it, the rumours, what the Watch Post foretold, what the Haven asked for and
/// wants), each its painting or picture with its name under it; the known dangers as the creatures' portraits (grey until met)
/// and the harms they deal; the likely finds. Every word about them is on hover. Part of RaidPreparationWindow, in its own file.
/// </summary>
public sealed partial class RaidPreparationWindow
{
    /// <summary>What one card shows, and what comes up when it is pointed at.</summary>
    private struct CardFace
    {
        public string Name;
        /// <summary>Cropped to fill the frame (an event's scene).</summary>
        public Sprite Painting;
        /// <summary>Laid whole on the card (a portrait, an item's icon).</summary>
        public Sprite Picture;
        /// <summary>A kit icon when there is no picture.</summary>
        public string Icon;
        public Color? IconTint;
        public string Badge;
        public Color? Rim;
        public bool Dim;
        public string TipTitle;
        public Color TipColour;
        public string TipBody;
        public Sprite TipIcon;
    }

    private readonly List<CardFace> eventFaces = new List<CardFace>();
    private readonly List<CardFace> dangerFaces = new List<CardFace>();

    // A large map's other regions (or a normal map's other districts, Rules › Normal Regions), named after the run's own.
    private string Beyond(RaidDefinition raid)
    {
        if (raid.LayoutRules == null)
        {
            return "";
        }

        bool large = plan.Large && raid.CanBeLarge;
        Vector2Int range = large ? raid.LayoutRules.LargeRegions : raid.LayoutRules.NormalRegions;
        if (range.y <= 1)
        {
            return "";
        }

        List<RaidMapMood> moods = RaidLayoutPlanner.RegionMoods(raid.LayoutRules, plan.Seed, range);
        if (moods.Count <= 1)
        {
            return "";
        }

        var others = new List<string>();
        for (int i = 1; i < moods.Count; i++)
        {
            others.Add(moods[i].name);
        }

        return $" Further off: {string.Join(" and ", others)}.";
    }

    // Once the run is laid out ahead (RaidLayoutPrefetch), whether there is a long way round as well as the short one.
    private string RoutesLine(RaidDefinition raid)
    {
        RaidLayoutPlan layout = RaidLayoutPrefetch.Peek(raid, plan.Seed);
        return layout != null && layout.LongRoute.Count > 0 ? " Two ways lead to the beacon ahead: the short one is watched, the long one quieter." : "";
    }

    // The left column. Each part sits under the last, so a run with one rumour and a tier with few monsters leave no gaps.
    // A place that can't be reached has no run to tell of, only what is known of it.
    private void DrawBriefing(RaidDefinition raid, RaidTimeOfDay timeOfDay, bool closed)
    {
        float y = BodyTop;
        int chipRows = DrawChips(raid, timeOfDay, closed, BodyTop + HeadingHeight);
        bool anyChip = chipRows > 0;
        CollectEvents(raid, closed);
        CollectDangers(raid);
        bool anything = anyChip || eventFaces.Count > 0;
        thisRunHeading.gameObject.SetActive(anything);
        if (anything)
        {
            y = Stack(thisRunHeading, y, HeadingHeight);
            y += chipRows * ChipRowHeight;
            // As many rows of events as the column has room for above the dangers and finds; the rest scroll.
            float room = BodyBottom - y - SectionGap - BelowEvents(raid);
            y = ShowCards(eventCards, eventFaces, eventView, eventContent, y, room, AddEventCard);
            y += SectionGap;
        }
        else
        {
            // A locked place, or nothing to tell: last place's cards mustn't stay (they overlapped the dangers, and hovering one threw).
            eventView.gameObject.SetActive(false);
        }

        dangersHeading.gameObject.SetActive(dangerFaces.Count > 0);
        if (dangerFaces.Count > 0)
        {
            y = Stack(dangersHeading, y, HeadingHeight);
            y = ShowCards(dangerCards, dangerFaces, dangerView, dangerContent, y, float.MaxValue, AddDangerCard) + SectionGap;
        }
        else
        {
            dangerView.gameObject.SetActive(false);
        }

        DrawFinds(raid, y);
    }

    // The hour, the map's mood and the sky the run opens in, each a short chip with its sign; what they mean on hover. A chip that
    // would run past the column starts a second row. Returns the rows used.
    private int DrawChips(RaidDefinition raid, RaidTimeOfDay timeOfDay, bool closed, float y)
    {
        UIStyle style = UIStyle.Current;
        float x = Margin;
        int rows = 0;
        float rowY = y;
        float Place(Chip chip, string sign, string text)
        {
            rows = Mathf.Max(rows, 1);
            float next = chip.Show(sign, text, x, rowY);
            if (next - 8f > Margin + ColumnWidth && x > Margin)
            {
                rows++;
                rowY += ChipRowHeight;
                next = chip.Show(sign, text, Margin, rowY);
            }

            return next;
        }

        hourChip.Root.SetActive(false);
        moodChip.Root.SetActive(false);
        skyChip.Root.SetActive(false);
        if (closed)
        {
            return 0;
        }

        if (timeOfDay != null)
        {
            int hours = Mathf.FloorToInt(timeOfDay.hour) % 24;
            int minutes = Mathf.FloorToInt((timeOfDay.hour - Mathf.Floor(timeOfDay.hour)) * 60f);
            string reward = timeOfDay.lootMultiplier > 1.001f ? $"  <color={UIStyle.Hex(style.Good)}>+{timeOfDay.lootMultiplier - 1f:P0} loot</color>" : "";
            bool dark = hours < 6 || hours >= 20;
            x = Place(hourChip, dark ? "Lantern" : "Sun", $"{timeOfDay.DisplayName} {hours:00}:{minutes:00}{reward}");
            hourDetail = timeOfDay.briefing;
        }

        if (raid.LayoutRules != null)
        {
            (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, plan.Seed);
            x = Place(moodChip, "Path", mood.name);
            moodDetail = $"{mood.briefing} {shape.briefing}{Beyond(raid)}{RoutesLine(raid)}";
            moodTitle = "The " + raid.Land;
        }

        // The sky the run opens in and what it does (research W3), as the raid will begin in it (RaidWeather, the same seed).
        // No snow: the Haven's winter snows, a raid doesn't (RaidSky sets no snowfall), and a rainy opening was briefed as snow.
        WeatherDefinition opening = RaidWeather.Opening(raid, plan.Seed);
        string sky = RaidWeather.BriefingLine(opening, false, PerceptionSettings.Current);
        if (sky != null)
        {
            Place(skyChip, RaidWeather.IconFor(opening), RaidWeather.NameFor(opening, false));
            skyDetail = sky;
        }

        return rows;
    }

    // Everything out there this run, as cards: told first what is worst (one of the Lost walking it), then the rumours, what the
    // Watch Post saw, and what home asked for.
    private void CollectEvents(RaidDefinition raid, bool closed)
    {
        UIStyle style = UIStyle.Current;
        eventFaces.Clear();
        if (closed)
        {
            return;
        }

        LostOneSettings lostSettings = LostOneSettings.Current;
        LostOneRecord walker = RaidLostOnes.ForRun(raid, plan.Seed, plan.Difficulty != null ? plan.Difficulty.tier : raid.Tier);
        string restless = LostOneRules.BriefingLine(walker, raid.DisplayName, lostSettings);
        if (restless != null)
        {
            eventFaces.Add(new CardFace
            {
                Name = walker.FullName, Painting = lostSettings.BriefingArt, Icon = "Skull", Rim = style.Danger,
                TipTitle = walker.FullName, TipColour = style.Danger, TipBody = restless,
            });
        }

        PlayerCrafting crafting = plan.Player != null ? plan.Player.GetComponent<PlayerCrafting>() : null;
        foreach (RaidRumourDefinition rumour in RaidRumours.For(raid, plan.Seed, GameSession.Here, crafting, plan.Large ? raid.LargeRaid.widthScale : 1f))
        {
            RaidEventDefinition site = rumour.Site;
            string name = site != null ? site.DisplayName : "A rumour";
            eventFaces.Add(new CardFace
            {
                Name = name, Painting = site != null ? site.Artwork : null, Icon = "Eye",
                TipTitle = name, TipColour = style.Accent, TipBody = rumour.BriefingFor(GameSession.Here),
            });
        }

        foreach (RaidEventDefinition foretold in RaidForecast.For(raid, plan.Seed, HavenReadiness.ForetoldEvents))
        {
            string warning = string.IsNullOrWhiteSpace(foretold.Warning) ? "" : "\n\n" + foretold.Warning;
            eventFaces.Add(new CardFace
            {
                Name = foretold.DisplayName, Painting = foretold.Artwork, Icon = "Eye", Badge = "Seen",
                TipTitle = foretold.DisplayName, TipColour = style.Accent, TipBody = "Seen from the Watch Post." + warning,
            });
        }

        ItemInventory bag = plan.Player != null ? plan.Player.GetComponent<ItemInventory>() : null;
        foreach (HavenRequestDefinition request in HavenRequestBoard.Standing())
        {
            int home = HavenRequestBoard.AtHome(request, bag);
            eventFaces.Add(new CardFace
            {
                Name = $"{request.AskerName} asks", Picture = request.Wanted != null ? request.Wanted.Icon : null, Icon = "House",
                Badge = $"×{request.Count}", Rim = home >= request.Count ? style.Good : (Color?)null,
                TipTitle = $"{request.AskerName} asks for {request.WantedName}", TipColour = style.Accent, TipBody = request.BriefingLine(home),
                TipIcon = request.Wanted != null ? request.Wanted.Icon : null,
            });
        }

        Player player = plan.Player != null ? plan.Player.GetComponent<Player>() : null;
        List<WantedAtHome.Want> wants = player != null ? WantedAtHome.Now(player) : new List<WantedAtHome.Want>();
        for (int i = 0; i < wants.Count && i < MaxWantedCards; i++)
        {
            WantedAtHome.Want want = wants[i];
            bool likely = (want.Item != null && Contains(raid.LikelyItems, want.Item)) || (want.Resource != null && Contains(raid.LikelyResources, want.Resource));
            Sprite icon = want.Item != null ? want.Item.Icon : want.Resource != null ? want.Resource.Icon : null;
            string more = i == MaxWantedCards - 1 && wants.Count > MaxWantedCards ? $"\n\nAnd {wants.Count - MaxWantedCards} more things besides." : "";
            eventFaces.Add(new CardFace
            {
                Name = want.Name, Picture = icon, Icon = "House", Badge = $"×{want.Short}", Rim = likely ? style.Good : (Color?)null,
                TipTitle = "Wanted at home", TipColour = style.Accent, TipIcon = icon,
                TipBody = $"{want.Name}: {want.Short} more for {want.For} ({want.Have}/{want.Need})." +
                          (likely ? $"\n<color={style.GoodHex}>Likely to be found here.</color>" : "") + more,
            });
        }
    }

    private static bool Contains<T>(IEnumerable<T> list, T one) where T : Object
    {
        foreach (T entry in list ?? System.Array.Empty<T>())
        {
            if (entry == one)
            {
                return true;
            }
        }

        return false;
    }

    // The creatures this tier can meet, as their portraits (grey until met), then what hurts beyond blows (Docs/PlayerStatsResearch.md
    // #8-9) as a sign each, with the food or resistance that answers it on hover.
    private void CollectDangers(RaidDefinition raid)
    {
        UIStyle style = UIStyle.Current;
        dangerFaces.Clear();
        int tier = plan.Difficulty != null ? plan.Difficulty.tier : Tiers.Max;
        List<RaidHarms.Harm> harms = RaidHarms.For(raid, tier);
        foreach (MonsterDefinition monster in raid.KnownEnemies)
        {
            if (monster == null || (plan.Difficulty != null && !raid.CanMeet(monster, plan.Difficulty.tier)))
            {
                continue;
            }

            bool met = GameSession.Progress.HasMet(monster.Id);
            dangerFaces.Add(new CardFace
            {
                Name = monster.DisplayName, Picture = monster.Portrait, Icon = "Skull", Dim = !met, Badge = met ? null : "?",
                TipTitle = monster.DisplayName, TipColour = style.Text, TipIcon = monster.Portrait, TipBody = DescribeCreature(monster, harms),
            });
        }

        foreach (RaidHarms.Harm harm in harms)
        {
            string name = RaidHarms.NameOf(harm.Type);
            string answer = StatNames.Of(RaidHarms.ResistanceOf(harm.Type));
            Color colour = HarmColour(harm.Type, style);
            dangerFaces.Add(new CardFace
            {
                Name = name, Icon = HarmIcon(harm.Type), IconTint = colour, Rim = colour,
                TipTitle = name, TipColour = colour,
                TipBody = $"Dealt by {string.Join(", ", harm.From)}.\nAnswered by Warding food or {answer}.",
            });
        }

        if (RaidHarms.IsCold(PlayerWarmth.ColdNow()))
        {
            Color frost = HarmColour(DamageType.Frost, style);
            dangerFaces.Add(new CardFace
            {
                Name = "The cold", Icon = "Snowflake", IconTint = frost, Rim = frost,
                TipTitle = "The cold", TipColour = frost, TipBody = "It bites in the open this season.\nAnswered by warm clothes, a fire, Warding food or Frost Resistance.",
            });
        }
        else if (harms.Count == 0 && dangerFaces.Count > 0)
        {
            // Nothing burns, chills or poisons: which food suits a run of plain blows, said on the creatures themselves.
            CardFace first = dangerFaces[0];
            first.TipBody += $"\n\n<color={style.MutedHex}>Nothing out there burns, chills or poisons: Hearty or Fierce food.</color>";
            dangerFaces[0] = first;
        }
    }

    // What the player knows of a creature: only that it is out there until met; then what it is, how many have fallen and, once
    // enough have, how to live through it (JournalContent.KnowledgeOf, as the journal tells it), and what it deals beyond blows.
    private static string DescribeCreature(MonsterDefinition monster, List<RaidHarms.Harm> harms)
    {
        CharacterProgress progress = GameSession.Progress;
        var text = new System.Text.StringBuilder();
        if (!progress.HasMet(monster.Id))
        {
            text.Append("Not yet met\nIt is said to be out there. Meet it, and the journal will say more.");
        }
        else
        {
            int slain = progress.TimesSlain(monster.Id);
            text.Append(slain > 0 ? $"Slain {slain}" : "Met, not yet slain");
            if (!string.IsNullOrWhiteSpace(monster.Description))
            {
                text.Append('\n').Append(monster.Description);
            }

            if (JournalContent.KnowledgeOf(monster, progress) >= 2 && !string.IsNullOrWhiteSpace(monster.JournalAdvice))
            {
                text.Append($"\n\n<color={UIStyle.Current.AccentHex}>How to live through it:</color> {monster.JournalAdvice}");
            }
        }

        foreach (RaidHarms.Harm harm in harms)
        {
            if (Contains(harm.From, monster.DisplayName))
            {
                text.Append($"\n\nDeals {RaidHarms.NameOf(harm.Type)}: answered by Warding food or {StatNames.Of(RaidHarms.ResistanceOf(harm.Type))}.");
            }
        }

        return text.ToString();
    }

    private static bool Contains(IReadOnlyList<string> names, string name)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (names[i] == name)
            {
                return true;
            }
        }

        return false;
    }

    private static string HarmIcon(DamageType type) => type == DamageType.Fire ? "Flame" : type == DamageType.Frost ? "Snowflake" : "Drop";

    private static Color HarmColour(DamageType type, UIStyle style) => type switch
    {
        DamageType.Fire => Color.Lerp(style.Warning, style.Danger, 0.35f),
        DamageType.Frost => new Color(0.55f, 0.78f, 0.95f, 1f),
        _ => new Color(0.55f, 0.8f, 0.35f, 1f),
    };

    // How tall the dangers and finds below the events are: the events get what is left of the column.
    private float BelowEvents(RaidDefinition raid)
    {
        float below = 0f;
        if (dangerFaces.Count > 0)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth + CardGap) / (DangerCardWidth + CardGap)));
            int rows = (dangerFaces.Count + perRow - 1) / perRow;
            float card = dangerCards.Count > 0 ? dangerCards[0].Height : DangerPictureHeight + 40f;
            below += HeadingHeight + rows * (card + CardGap) - CardGap + SectionGap;
        }

        int finds = (raid.LikelyItems?.Length ?? 0) + (raid.LikelyResources?.Length ?? 0);
        if (finds > 0)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth + 6f) / (FindSize + 6f)));
            below += HeadingHeight + ((finds + perRow - 1) / perRow) * (FindSize + 6f);
        }

        return below;
    }

    // Shows the faces on a pool of cards (made as they are needed), in rows across the column, no taller than `room` (at least a
    // row): rows that don't fit scroll in their view, the next one peeking out as a sign. Returns the height below them.
    private float ShowCards(List<KitPictureCard> cards, List<CardFace> faces, RectTransform view, RectTransform holder, float y, float room,
        System.Func<int, KitPictureCard> add)
    {
        view.gameObject.SetActive(faces.Count > 0);
        while (cards.Count < faces.Count)
        {
            cards.Add(add(cards.Count));
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < faces.Count;
            cards[i].SetActive(used);
            if (used)
            {
                Bind(cards[i], faces[i]);
            }
        }

        if (faces.Count == 0)
        {
            return y;
        }

        float height = KitPictureCard.Lay(cards, faces.Count, 0f, 0f, ColumnWidth, CardGap);
        float row = cards[0].Height + CardGap;
        float shown = height;
        if (height > room)
        {
            // Whole rows that fit, and a glimpse of the next where there is room for one.
            int rows = Mathf.Max(1, Mathf.FloorToInt((room + CardGap) / row));
            shown = rows * row - CardGap;
            if (room - shown >= RowPeek)
            {
                shown += RowPeek;
            }
        }

        holder.sizeDelta = new Vector2(ColumnWidth, height);
        holder.anchoredPosition = Vector2.zero;
        Stack(view, y, shown);
        return y + shown;
    }

    private static void Bind(KitPictureCard card, CardFace face)
    {
        card.SetName(face.Name);
        if (face.Painting != null)
        {
            card.ShowPainting(face.Painting);
        }
        else
        {
            card.ShowPicture(face.Picture, face.Icon, face.IconTint);
        }

        card.SetBadge(face.Badge);
        card.SetLook(face.Dim, face.Rim);
    }

    private void ShowTip(CardFace face)
    {
        if (face.TipIcon != null)
        {
            tooltip.Show(face.TipTitle, face.TipColour, face.TipBody, face.TipIcon);
        }
        else
        {
            tooltip.Show(face.TipTitle, face.TipColour, face.TipBody);
        }
    }

    private void DrawFinds(RaidDefinition raid, float y)
    {
        var finds = new List<(Sprite icon, UnityEngine.Object definition)>();
        foreach (ItemDefinition item in raid.LikelyItems)
        {
            if (item != null && (plan.Difficulty == null || plan.Difficulty.Allows(item)))
            {
                finds.Add((item.Icon, item));
            }
        }

        foreach (ResourceDefinition resource in raid.LikelyResources)
        {
            if (resource != null)
            {
                finds.Add((resource.Icon, resource));
            }
        }

        while (findSlots.Count < finds.Count)
        {
            findSlots.Add(AddFindSlot());
        }

        findsHeading.gameObject.SetActive(finds.Count > 0);
        if (finds.Count > 0)
        {
            y = Stack(findsHeading, y, HeadingHeight);
        }

        const float step = FindSize + 6f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((ColumnWidth + 6f) / step));
        for (int i = 0; i < findSlots.Count; i++)
        {
            FindSlot slot = findSlots[i];
            slot.Rect.gameObject.SetActive(i < finds.Count);
            if (i < finds.Count)
            {
                slot.Definition = finds[i].definition;
                slot.Icon.sprite = finds[i].icon;
                slot.Icon.enabled = finds[i].icon != null;
                slot.Rect.anchoredPosition = new Vector2(Margin + i % perRow * step, -(y + i / perRow * step));
            }
        }
    }
}
