using System;
using System.Collections.Generic;
using Researchers;
using GameDataObjects;
using Unity.U2D.Physics;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Generates the starting pool of researchers from the game seed.
/// This only creates the data object, not game object.
/// STARTER PICK: the starting researchers are the "Starting Researchers" list in this component's
/// Inspector (name, description, speed, quality, methodologies in unlock order, look). The start screen shows the same list.
/// They're added after the random pool, so their IDs are researcherCount + 1, + 2, ...
/// (9 and 10 with the default 8, the same IDs as before).
/// </summary>
public class ResearcherGenerator : MonoBehaviour
{
    // Stream ID for this system's random numbers (see GameSeed.CreateRandom).
    private const int RandomStreamId = 1001;
    

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = false;
    [SerializeField, Min(1)] private int researcherCount = 8;
    [Tooltip("Optional prefab with a Researcher component. If empty, a new GameObject is created.")]
    [SerializeField] private Researcher researcherPrefab;
    [SerializeField] private Transform spawnParent;

    [Header("Appearance (number of assets available per slot)")]
    [Tooltip("Must be >= Researcher Count so every head is unique.")]
    [SerializeField, Min(1)] private int headCount = 12;
    [SerializeField, Min(1)] private int bodyCount = 6;
    [SerializeField, Min(1)] private int legCount = 8;

    [Header("Stat ranges (inclusive)")]
    [SerializeField] private Vector2Int speedRange = new Vector2Int(1, 3);
    [SerializeField] private Vector2Int qualityRange = new Vector2Int(1, 3);

    [Header("Methodologies")]
    [Tooltip("Ticked: hand out every methodology in methodologies.csv (new rows are picked up automatically). " +
             "Unticked: only hand out the IDs in the list below.")]
    [SerializeField] private bool useAllConfigMethodologies = true;
    [Tooltip("Only used when the box above is unticked. IDs from the ID column of methodologies.csv.")]
    [SerializeField] private List<int> methodologyIds = new List<int>();
    [Tooltip("How many methodologies each researcher knows. Lowered automatically if there are fewer in the game.")]
    [SerializeField, Min(1)] private int methodologiesPerResearcher = 4;

    // EDIT THE STARTING RESEARCHERS HERE.
    [Header("Starting researchers (the start screen shows one card per entry)")]
    [Tooltip("The researchers the player picks from on the start screen. Add an entry with + to add a card.")]
    [SerializeField] private List<StarterResearcher> startingResearchers = new List<StarterResearcher>
    {
        new StarterResearcher("James Fitzgerald",
            2, 1, new Vector3Int(0, 0, 0),
            "Product R&D", "Market Research", "Exploratory Research", "Randomized Controlled Trial"),
        new StarterResearcher("Maya Nobel",
            1, 2, new Vector3Int(1, 1, 1),
            "Field Research", "Exploratory Research", "Longitudinal Research", "Policy Research"),
    };

    [Tooltip("Ticked: the starters the player didn't pick show up in the shop's hire tab like everyone else. " +
             "Unticked: they're never offered.")]
    [SerializeField] private bool unpickedStartersCanBeHired = true;

    /// <summary>The starters, in Inspector order (= the order of the start screen cards).</summary>
    public IReadOnlyList<StarterResearcher> StartingResearchers => startingResearchers;



    [Header("Name pools")]
    [SerializeField] private string[] firstNames =
    {
        "Jim","Stephen","Brandon","Yao","Minwoo","Ashten", "Ruby", "Phineas", "Ferb", "Dwight", "Sam", "John", "Kim", "Chloe", "Michelle", "Laufey", "Lara", "Erica"
    };
    [SerializeField] private string[] lastNames =
    {
        "Lathrop", "Gilbert", "D'Costa","Amegandjin", "Kim", "Finders","King", "Flynn", "Smith", "Johnson", "Anderson", "Gonzalez", "Garcia"
    };

    [Header("Researcher List")]
    private IReadOnlyList<ResearcherData> researchers = Array.Empty<ResearcherData>();
    public IReadOnlyList<ResearcherData> Researchers => researchers;

    //Give the researcher generator read only reference to the datamanager's researchers!
    public void Initialize(IReadOnlyList<ResearcherData> researchers)
    {
        this.researchers = researchers;
    }
    private void Start()
    {
        if (generateOnStart)
            Generate();

    }

    /// <summary>Destroys any existing generated researchers and creates a fresh set from the seed.</summary>
    public List<ResearcherData> Generate()
    {
        if (!ValidateSettings()) return null;

        // the methodologies come from the config file now, so new ones show up without touching this script
        if (useAllConfigMethodologies)
            methodologyIds = MethodLibrary.GetAllIds();
        if (!ValidateMethodologies()) return null;

        var researchers = new List<ResearcherData>();



        Random rng = GameSeed.CreateRandom(RandomStreamId);
        Debug.Log($"ResearcherGenerator: generating with seed {GameSeed.Seed}");

        //Unique heads: shuffle all head indices, then deal one to each researcher.
        List<int> heads = new List<int>();
        for (int h = 0; h < headCount; h++) heads.Add(h);
        Shuffle(heads, rng);

        //Pool of methodologies not yet handed out in the current cycle.
        List<int> unused = new List<int>();
        RefillPool(unused, rng);

        HashSet<string> usedNames = new HashSet<string>();
        
        for (int i = 0; i < researcherCount; i++)
        {
            string fullName = GenerateUniqueName(rng, usedNames);
            Vector3Int look = new Vector3Int(heads[i], rng.Next(bodyCount), rng.Next(legCount));

            int experience = 0;
            int speed = NextInclusive(rng, speedRange);
            int quality = NextInclusive(rng, qualityRange);
            
            List<int> methods = DrawMethodologies(unused, rng);

            // Sequential IDs: 1 through researcherCount.
            int id = i + 1;

            ResearcherData r = new ResearcherData();
            r.Init(id, fullName, look, experience, speed, quality,methods);
            researchers.Add(r);
            
            Debug.Log($"ResearcherGenerator: Researcher generated: " + r.Name);
            
        }
        // STARTER PICK: one researcher per entry in the Starting Researchers list, after the random pool.
        // Made after the pool so the pool's random numbers (and so its names, looks and methods) are the same as before
        for (int i = 0; i < startingResearchers.Count; i++)
        {
            StarterResearcher s = startingResearchers[i];
            if (s == null) continue;

            // their Inspector methods first, in unlock order, then random ones if fewer than 4 were listed
            List<int> methods = DrawMethodologies(unused, rng, s.GetMethodologyIds(warn: true));

            ResearcherData starter = new ResearcherData();
            starter.Init(StarterId(i), s.name, s.appearance, 0, s.speed, s.quality, methods);
            researchers.Add(starter);
            Debug.Log($"ResearcherGenerator: starter generated: {s.name} (ID {StarterId(i)})");
        }

        return researchers;
    }

    // STARTER PICK
    // starters come right after the random pool: with 8 researchers, the first starter is 9, the second 10, ...
    private int StarterId(int starterIndex) => researcherCount + 1 + starterIndex;

    // STARTER PICK
    /// <summary>The researcher made from a start screen card (0 = the first entry in Starting Researchers), or null.</summary>
    public ResearcherData GetStarter(int starterIndex)
    {
        if (starterIndex < 0 || starterIndex >= startingResearchers.Count) return null;
        return GetById(StarterId(starterIndex));
    }

    // STARTER PICK
    /// <summary>True for researchers made from the Starting Researchers list (their IDs come after the random pool).</summary>
    public bool IsStarter(ResearcherData researcher)
    {
        return researcher != null && researcher.Id > researcherCount;
    }
    
    /// <summary>Finds a researcher by ID (1-10), or null.</summary>
    public ResearcherData GetById(int id)
    {
        foreach (ResearcherData r in researchers)
            if (r.Id == id) return r;
        return null;
    }

    public List<ResearcherData> GetHiredResearchers()
    {
        List<ResearcherData> result = new List<ResearcherData>();
        foreach (ResearcherData r in researchers)
        {
            if (r.IsPurchased)
            {
                result.Add(r);
            }
        }
        return result;
    }
 
    /// <summary>Researchers who haven't been hired yet.</summary>
    public List<ResearcherData> GetUnhired()
    {
        List<ResearcherData> result = new List<ResearcherData>();
        foreach (ResearcherData r in researchers)
        {
            if (r.IsPurchased) continue;
            if (!unpickedStartersCanBeHired && IsStarter(r)) continue;
            result.Add(r);
        }
        return result;
    }
    
    
    /// <summary>
    /// Snapshots of all researchers for saving. These are copies, so the game does not change them.
    /// We save the whole list, since stats may change.
    /// </summary>
    public ResearcherList GetState()
    {
        ResearcherList researcherList = new ResearcherList();
        foreach (ResearcherData r in researchers)
            researcherList.researchers.Add(r.Clone());
        return researcherList;
    }
 


    // Methodology distribution

    /// <summary>
    /// Draws methodologies for one researcher. The order of the returned list is the
    /// level at which each is unlocked (index 0 = level 1).
    /// Uses the "unused" pool so every methodology is handed out once before any repeats,
    /// giving an even spread across researchers. A researcher never gets the same one twice.
    /// </summary>
    private List<int> DrawMethodologies(List<int> unused, Random rng, IEnumerable<int> alreadyKnown = null)
    {

        List<int> unlocked = alreadyKnown != null ? new List<int>(alreadyKnown) : new List<int>();

        // can't know more methods than exist (with 3 methods and 4 wanted, this loop would never end)
        int wanted = Mathf.Min(methodologiesPerResearcher, new HashSet<int>(methodologyIds).Count);
        // a starter can list more than "Methodologies Per Researcher". Keep them all rather than cut some off
        if (unlocked.Count > wanted) return unlocked;

        while (unlocked.Count < wanted)
        {
            // Start a new cycle (every methodology has now been used equally often).
            if (unused.Count == 0)
                RefillPool(unused, rng);
            
            //finds the first index in unused, where it also is not in unlocked
            int index = unused.FindIndex(m => !unlocked.Contains(m));

            // Only duplicates of this researcher's methodologies are left; start a new cycle.
            if (index < 0)
            {
                RefillPool(unused, rng);
                index = unused.FindIndex(m => !unlocked.Contains(m));
                // STARTER PICK (new): a starter's Inspector methods can include IDs that aren't in this pool
                // (when Use All Config Methodologies is unticked), so the pool can run out. Stop instead of crashing
                if (index < 0) break;
            }

            unlocked.Add(unused[index]);
            unused.RemoveAt(index);
        }

        return unlocked;
    }

    /// <summary>Resets the pool to the full methodology list, shuffled so picks are random.</summary>
    private void RefillPool(List<int> pool, Random rng)
    {
        pool.Clear();
        pool.AddRange(methodologyIds);
        Shuffle(pool, rng);
    }

    // Helpers

    private string GenerateUniqueName(Random rng, HashSet<string> used)
    {
        //Retry a bounded number of times, then fall back to appending a number.
        for (int attempt = 0; attempt < 100; attempt++)
        {
            string candidate = $"{firstNames[rng.Next(firstNames.Length)]} {lastNames[rng.Next(lastNames.Length)]}";
            if (used.Add(candidate))
                return candidate;
        }

        string fallback = $"{firstNames[0]} {lastNames[0]} {used.Count + 1}";
        used.Add(fallback);
        return fallback;
    }


    

    //Inclusive min/max; tolerates a reversed range.
    private static int NextInclusive(Random rng, Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return rng.Next(min, max + 1);
    }

    //Shuffles using the seed generator.
    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // checked after the IDs are read from the config, so it's separate from ValidateSettings
    private bool ValidateMethodologies()
    {
        if (methodologyIds == null || methodologyIds.Count == 0)
        {
            Debug.LogError(useAllConfigMethodologies
                ? "ResearcherGenerator: no methodologies loaded. Check Assets/Resources/Config/methodologies.csv."
                : "ResearcherGenerator: methodologyIds is empty. Add IDs or tick Use All Config Methodologies.");
            return false;
        }
        foreach (int id in methodologyIds)
        {
            if (MethodLibrary.Get(id) == null)
                Debug.LogWarning($"ResearcherGenerator: methodology ID {id} isn't in methodologies.csv. Researchers may show \"Unknown\".");
        }
        return true;
    }

    private bool ValidateSettings()
    {
        if (headCount < researcherCount)
        {
            Debug.LogError($"ResearcherGenerator: headCount ({headCount}) must be >= researcherCount ({researcherCount}) so heads are unique.");
            return false;
        }
        if (firstNames.Length == 0 || lastNames.Length == 0)
        {
            Debug.LogError("ResearcherGenerator: name pools must not be empty.");
            return false;
        }
        return true;
    }
}