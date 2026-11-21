using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's crafting knowledge and hand crafting: which recipes and buildings they know, whether a By Hand recipe
/// can be made right now, and making it. A hand recipe needs to be known, its costs in the MaterialPool (what is carried,
/// plus storage at home) and room for the result; the player's crafts from the bag take time (HandCraftQueue: paid when
/// asked for, made after HandCraftingSettings' seconds), TryCraft makes one at once: items go to the bag, resources to the
/// resource bars (at home, overflow goes to storage). Every other recipe is queued at its crafting station (StationCrafting,
/// StationQueue) and Check explains where it is made.
/// <para>
/// Learning: recipes and buildings that aren't known from the start are learned through Unlock / UnlockBuildings, from a
/// blueprint, a resident's job or a boss. Known recipes and buildings are kept on death (they are knowledge) and saved.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceInventory), typeof(ItemInventory))]
public sealed class PlayerCrafting : MonoBehaviour
{
    /// <summary>Whether a recipe can be crafted right now, and if not, why, in words for the player.</summary>
    public readonly struct CraftCheck
    {
        public static readonly CraftCheck Ok = new CraftCheck(true, null);

        public readonly bool CanCraft;
        public readonly string Problem;

        public CraftCheck(bool canCraft, string problem)
        {
            CanCraft = canCraft;
            Problem = problem;
        }

        public static CraftCheck Fail(string problem) => new CraftCheck(false, problem);
    }

    [SerializeField] private RecipeBook recipeBook;
    [Tooltip("How close the player must stand to a crafting station, in meters.")]
    [SerializeField, Min(0.5f)] private float stationRange = 4f;

    private readonly HashSet<RecipeDefinition> known = new HashSet<RecipeDefinition>();
    private readonly HashSet<BuildingDefinition> knownBuildings = new HashSet<BuildingDefinition>();
    private ItemInventory items;
    private bool initialised;

    public IReadOnlyList<RecipeDefinition> Recipes =>
        recipeBook != null ? recipeBook.Recipes : (IReadOnlyList<RecipeDefinition>)Array.Empty<RecipeDefinition>();

    /// <summary>Every building in the game, known or not, for the build menu.</summary>
    public IReadOnlyList<BuildingDefinition> Buildings =>
        recipeBook != null ? recipeBook.Buildings : (IReadOnlyList<BuildingDefinition>)Array.Empty<BuildingDefinition>();

    /// <summary>Every recipe the player knows, including the ones known from the start.</summary>
    public IReadOnlyCollection<RecipeDefinition> KnownRecipes
    {
        get
        {
            EnsureInitialised();
            return known;
        }
    }

    /// <summary>Every building the player knows how to build, including the ones known from the start.</summary>
    public IReadOnlyCollection<BuildingDefinition> KnownBuildings
    {
        get
        {
            EnsureInitialised();
            return knownBuildings;
        }
    }

    /// <summary>What the player can spend right now: carried things, plus storage at home.</summary>
    public MaterialPool Pool => MaterialPool.For(gameObject);

    /// <summary>Raised after new recipes or buildings are learned.</summary>
    public event Action KnownRecipesChanged;

    /// <summary>A building learned with an announcement (not one restored from a save), by whom: BigNotice shows the local player's.</summary>
    public static event Action<PlayerCrafting, BuildingDefinition> BuildingLearned;

    // Entering Play doesn't reload scripts in this project: listeners of the last session go with it.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => BuildingLearned = null;

    /// <summary>Raised after something is crafted.</summary>
    public event Action<RecipeDefinition> Crafted;

    private void Awake()
    {
        EnsureInitialised();
    }

    /// <summary>True when the player already knows everything an item would teach them.</summary>
    public bool KnowsAll(ItemDefinition teacher)
    {
        foreach (BuildingDefinition building in teacher.TeachesBuildings)
        {
            if (building != null && !IsKnown(building))
            {
                return false;
            }
        }

        foreach (RecipeDefinition recipe in teacher.TeachesRecipes)
        {
            if (recipe != null && !IsKnown(recipe))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsKnown(RecipeDefinition recipe)
    {
        EnsureInitialised();
        return recipe != null && known.Contains(recipe);
    }

    public bool IsKnown(BuildingDefinition building)
    {
        EnsureInitialised();
        return building != null && knownBuildings.Contains(building);
    }

    /// <summary>
    /// Learns the recipes the player doesn't know yet and, unless told not to (restoring saved knowledge), announces them.
    /// Returns how many were new.
    /// </summary>
    public int Unlock(IEnumerable<RecipeDefinition> recipes, bool announce = true)
    {
        EnsureInitialised();
        var learned = new List<string>();
        foreach (RecipeDefinition recipe in recipes)
        {
            if (recipe != null && known.Add(recipe))
            {
                learned.Add(recipe.DisplayName);
            }
        }

        return Announce(learned, "New recipes: ", announce);
    }

    /// <summary>Learns buildings the player doesn't know yet, announcing them unless restoring. Returns how many were new.</summary>
    public int UnlockBuildings(IEnumerable<BuildingDefinition> buildings, bool announce = true)
    {
        EnsureInitialised();
        var learned = new List<string>();
        var learnedBuildings = announce ? new List<BuildingDefinition>() : null;
        foreach (BuildingDefinition building in buildings)
        {
            if (building != null && knownBuildings.Add(building))
            {
                learned.Add(building.DisplayName);
                learnedBuildings?.Add(building);
            }
        }

        int count = Announce(learned, "New building plans: ", announce);
        foreach (BuildingDefinition building in learnedBuildings ?? (IEnumerable<BuildingDefinition>)Array.Empty<BuildingDefinition>())
        {
            BuildingLearned?.Invoke(this, building);
        }

        return count;
    }

    private int Announce(List<string> learned, string prefix, bool announce)
    {
        if (learned.Count > 0)
        {
            if (announce)
            {
                GameMessages.Post(prefix + string.Join(", ", learned), MessageKind.Gain);
            }

            KnownRecipesChanged?.Invoke();
        }

        return learned.Count;
    }

    public bool IsNearStation(CraftingStationType station)
    {
        return CraftingStation.IsNear(transform.position, station, stationRange);
    }

    /// <summary>How much of a resource can be spent right now (carried, plus storage at home).</summary>
    public int Have(ResourceDefinition resource)
    {
        EnsureInitialised();
        return Pool.Have(resource);
    }

    /// <summary>How many of an item can be spent right now (bag, plus storage at home).</summary>
    public int Have(ItemDefinition item)
    {
        EnsureInitialised();
        return Pool.Have(item);
    }

    public CraftCheck Check(RecipeDefinition recipe)
    {
        EnsureInitialised();
        if (recipe == null || !recipe.HasOutput)
        {
            return CraftCheck.Fail("This recipe makes nothing.");
        }

        if (!known.Contains(recipe))
        {
            return CraftCheck.Fail("Not learned yet.");
        }

        if (!recipe.IsHandcrafted)
        {
            return CraftCheck.Fail($"Made at {recipe.Station.WithArticle()}.");
        }

        if (recipe.SkillProblem(GameSession.Progress) is string unskilled)
        {
            return CraftCheck.Fail(unskilled);
        }

        MaterialPool pool = Pool;
        if (!pool.CanAfford(recipe.ResourceCost, recipe.ItemCost, out string missing))
        {
            return CraftCheck.Fail(missing);
        }

        if (recipe.Output != null && items.Container.SpaceFor(recipe.Output) < recipe.OutputAmount)
        {
            return CraftCheck.Fail("No room in the bag.");
        }

        if (recipe.OutputResource != null && pool.FreeSpaceFor(recipe.OutputResource) < recipe.OutputAmount)
        {
            return CraftCheck.Fail($"No room for more {recipe.OutputResource.DisplayName}.");
        }

        return CraftCheck.Ok;
    }

    /// <summary>How many times the recipe can be crafted right now, with what is held and the room for what it makes.</summary>
    public int CraftableCount(RecipeDefinition recipe)
    {
        if (!Check(recipe).CanCraft)
        {
            return 0;
        }

        MaterialPool pool = Pool;
        int count = RecipeMaking.Batches(recipe, pool.Have, pool.Have);
        int room = recipe.Output != null ? items.Container.SpaceFor(recipe.Output) : pool.FreeSpaceFor(recipe.OutputResource);
        return Mathf.Min(count, room / Mathf.Max(1, recipe.OutputAmount));
    }

    /// <summary>Crafts the recipe at once if possible (tests, and anything that hands a craft over whole); otherwise tells the
    /// player why not. Returns true when it was crafted. The player's own crafting from the bag takes time: HandCraftQueue.</summary>
    public bool TryCraft(RecipeDefinition recipe)
    {
        if (!TryPay(recipe))
        {
            return false;
        }

        Finish(recipe);
        return true;
    }

    /// <summary>Whether what one craft of <paramref name="recipe"/> makes would fit now (a paid craft waits for room rather than
    /// being made into a full bag and lost).</summary>
    public bool HasRoomFor(RecipeDefinition recipe)
    {
        if (recipe == null)
        {
            return false;
        }

        if (recipe.Output != null)
        {
            return items.Container.SpaceFor(recipe.Output) >= recipe.OutputAmount;
        }

        return recipe.OutputResource == null || Pool.FreeSpaceFor(recipe.OutputResource) >= recipe.OutputAmount;
    }

    /// <summary>Takes what one craft costs, if it can be made now; otherwise tells the player why not.</summary>
    public bool TryPay(RecipeDefinition recipe) => TryPay(recipe, out _);

    /// <summary>TryPay, noting what came from where (bag, cart, stash) for a refund.</summary>
    public bool TryPay(RecipeDefinition recipe, out MaterialPool.Payment paid)
    {
        paid = null;
        CraftCheck check = Check(recipe);
        if (!check.CanCraft)
        {
            GameMessages.Post(check.Problem, MessageKind.Warning);
            return false;
        }

        return Pool.TrySpend(recipe.ResourceCost, recipe.ItemCost, out paid);
    }

    /// <summary>Gives back what one craft of <paramref name="recipe"/> cost (a craft that was paid and not finished).</summary>
    public void GiveBack(RecipeDefinition recipe)
    {
        if (recipe != null)
        {
            Pool.GiveBack(recipe.ResourceCost, recipe.ItemCost, transform);
        }
    }

    /// <summary>Gives back a payment where it came from (a craft that was paid and not finished).</summary>
    public void GiveBack(MaterialPool.Payment paid)
    {
        Pool.GiveBack(paid, transform);
    }

    /// <summary>Makes one paid craft of <paramref name="recipe"/>: its result, the skill it trains, and the Crafted events.</summary>
    public void Finish(RecipeDefinition recipe)
    {
        MaterialPool pool = Pool;
        if (recipe.Output != null)
        {
            // Made by hand: quality, condition and any extra come from the player's crafting skill, which it trains.
            CraftedWork.GiveTo(items, recipe.Output, recipe.OutputAmount, TryGetComponent(out Combatant self) ? self.Stats : null);
            ExperienceTracker.PractiseCraft(recipe);
        }
        else
        {
            pool.Add(recipe.OutputResource, recipe.OutputAmount);
            GameMessages.Post($"+{recipe.OutputAmount} {recipe.OutputResource.DisplayName}", MessageKind.Gain);
        }

        Crafted?.Invoke(recipe);
        GameEvents.Raise(new GameEvent(GameEventKind.Crafted, recipe.OutputDefinition, recipe.OutputAmount, gameObject));
    }

    private void EnsureInitialised()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        items = GetComponent<ItemInventory>();
        foreach (RecipeDefinition recipe in Recipes)
        {
            if (recipe != null && recipe.KnownFromStart)
            {
                known.Add(recipe);
            }
        }

        foreach (BuildingDefinition building in Buildings)
        {
            if (building != null && building.KnownFromStart)
            {
                knownBuildings.Add(building);
            }
        }
    }
}