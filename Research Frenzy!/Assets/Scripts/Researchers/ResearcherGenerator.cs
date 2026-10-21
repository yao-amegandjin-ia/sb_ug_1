using System;
using System.Collections.Generic;
using Researchers;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Generates the starting pool of researchers from the game seed.
/// This only creates the data object, not game object.
/// </summary>
public class ResearcherGenerator : MonoBehaviour
{
    // Stream ID for this system's random numbers (see GameSeed.CreateRandom).
    private const int RandomStreamId = 1001;

    [Header("Seed")]
    [Tooltip("If ticked, uses the seed below and stores it as the game seed. " +
             "Otherwise uses the existing game seed (or makes a new one).")]
    [SerializeField] private bool overrideSeed = false;
    [SerializeField] private int seedOverride = 12345;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField, Min(1)] private int researcherCount = 10;
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
    [Tooltip("IDs of every methodology in the game.")]
    [SerializeField] private List<int> methodologyIds = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
    



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
    [SerializeField] private List<ResearcherData> researchers = new List<ResearcherData>();
    public IReadOnlyList<ResearcherData> Researchers => researchers;

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    /// <summary>Destroys any existing generated researchers and creates a fresh set from the seed.</summary>
    public void Generate()
    {
        if (!ValidateSettings()) return;
        
        researchers.Clear();

        if (overrideSeed)
            GameSeed.Set(seedOverride);
        
        Random rng = GameSeed.CreateRandom(RandomStreamId);
        Debug.Log($"ResearcherGenerator: generating with seed {GameSeed.Seed} (override {(overrideSeed ? "ON" : "OFF")}).");

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
    }
    
    /// <summary>Finds a researcher by ID (1-10), or null.</summary>
    public ResearcherData GetById(int id)
    {
        foreach (ResearcherData r in researchers)
            if (r.Id == id) return r;
        return null;
    }
 
    /// <summary>Researchers who haven't been hired yet.</summary>
    public List<ResearcherData> GetUnhired()
    {
        List<ResearcherData> result = new List<ResearcherData>();
        foreach (ResearcherData r in researchers)
            if (!r.IsPurchased) result.Add(r);
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
 
    /// <summary>
    /// This replaces the researcher list after loading.
    /// </summary>
    public void LoadState(ResearcherList researcherList)
    {
        if (researcherList == null)
        {
            Debug.LogError("ResearcherGenerator: cannot load a null state.");
            return;
        }
 
        researchers.Clear();
        foreach (ResearcherData r in researcherList.researchers)
            researchers.Add(r.Clone());
    }



    // Methodology distribution

    /// <summary>
    /// Draws methodologies for one researcher. The order of the returned list is the
    /// level at which each is unlocked (index 0 = level 1).
    /// Uses the "unused" pool so every methodology is handed out once before any repeats,
    /// giving an even spread across researchers. A researcher never gets the same one twice.
    /// </summary>
    private List<int> DrawMethodologies(List<int> unused, Random rng)
    {

        List<int> unlocked = new List<int>();

        while (unlocked.Count < 4)
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

    private bool ValidateSettings()
    {
        if (headCount < researcherCount)
        {
            Debug.LogError($"ResearcherGenerator: headCount ({headCount}) must be >= researcherCount ({researcherCount}) so heads are unique.");
            return false;
        }
        if (methodologyIds == null || methodologyIds.Count == 0)
        {
            Debug.LogError("ResearcherGenerator: methodologyIds is empty.");
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