using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Generates the starting pool of researchers from the game seed.
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
    [SerializeField] private Vector2Int experienceRange = new Vector2Int(0, 10);
    [SerializeField] private Vector2Int speedRange = new Vector2Int(1, 3);
    [SerializeField] private Vector2Int qualityRange = new Vector2Int(1, 3);

    [Header("Methodologies")]
    [Tooltip("IDs of every methodology in the game.")]
    [SerializeField] private List<int> methodologyIds = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
    [Tooltip("How many methodologies (levels) each researcher starts with (inclusive).")]
    [SerializeField] private Vector2Int methodologiesPerResearcher = new Vector2Int(1, 4);

    [Header("Name pools")]
    [SerializeField] private string[] firstNames =
    {
        "Jim","Stephen","Brandon","Yao","Minwoo","Ashten", "Ruby", "Phineas", "Ferb", "Dwight", "Sam", "John", "Kim", "Chloe", "Michelle", "Laufey", "Lara", "Erica"
    };
    [SerializeField] private string[] lastNames =
    {
        "Lathrop", "Gilbert", "D'Costa","Amegandjin", "Kim", "Finders","King", "Flynn", "Smith", "Johnson", "Anderson", "Gonzalez", "Garcia"
    };

    private readonly List<Researcher> researchers = new List<Researcher>();
    public IReadOnlyList<Researcher> Researchers => researchers;

    private void Start()
    {
        if (generateOnStart)
            Generate();
    }

    /// <summary>Destroys any existing generated researchers and creates a fresh set from the seed.</summary>
    public void Generate()
    {
        if (!ValidateSettings()) return;

        foreach (Researcher old in researchers)
            if (old != null) Destroy(old.gameObject);
        researchers.Clear();

        if (overrideSeed)
            GameSeed.Set(seedOverride);
        Random rng = GameSeed.CreateRandom(RandomStreamId);

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

            int methodCount = NextInclusive(rng, methodologiesPerResearcher);
            List<int> methods = DrawMethodologies(methodCount, unused, rng);

            // Sequential IDs: 1 through researcherCount.
            int id = i + 1;

            Researcher r = CreateResearcherObject(fullName);
            r.Init(id, fullName, look, experience, speed, quality,methods);
            researchers.Add(r);
        }
    }

    // Methodology distribution

    /// <summary>
    /// Draws methodologies for one researcher. The order of the returned list is the
    /// level at which each is unlocked (index 0 = level 1).
    /// Uses the "unused" pool so every methodology is handed out once before any repeats,
    /// giving an even spread across researchers. A researcher never gets the same one twice.
    /// </summary>
    private List<int> DrawMethodologies(int count, List<int> unused, Random rng)
    {
        count = Mathf.Min(count, methodologyIds.Count);
        List<int> unlocked = new List<int>();

        while (unlocked.Count < count)
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

    private Researcher CreateResearcherObject(string objectName)
    {
        if (researcherPrefab != null)
        {
            Researcher instance = Instantiate(researcherPrefab, spawnParent);
            instance.gameObject.name = objectName;
            return instance;
        }

        GameObject go = new GameObject(objectName);
        if (spawnParent != null) go.transform.SetParent(spawnParent);
        return go.AddComponent<Researcher>();
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