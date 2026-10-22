using Assets;
using Assets.Scripts.Bootstrap;
using Assets.Scripts.Clients;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using UnityEngine;
using System;
using System.Collections.Generic;
using System;
using Clients;
using Researchers;


/// <summary>One finished study: which methodology the player matched a request with.</summary>
[Serializable]
public class StudyResult
{
    public int RequestId;
    public int MethodologyId;
}
 
/// <summary>Wrapper so JsonUtility can save the list (it can't save a list or dictionary on its own).</summary>
[Serializable]
public class StudyResultList
{
    public List<StudyResult> Results = new List<StudyResult>();
}

/// <summary>
/// Runs at the start of the game
/// Acts as a controller for the game
/// </summary>

public class GameInitializer : MonoBehaviour
{
    public int day = 1;   // placeholder until the day system exists
    
    [Header("Seed")]
    [Tooltip("If ticked, uses the seed below as the game seed. Otherwise a random seed is made at startup.")]
    [SerializeField] private bool overrideSeed = false;
    [SerializeField] private int seedOverride = 12345;
 
    /// <summary>
    /// Current money is the current money the player has
    /// Total money is the total amount earned in the game. THIS DOES NOT GO DOWN.
    /// Total money is used for scoring
    /// </summary>
    [Header("Money")]
    
    
    [SerializeField] public int currentMoney = 0;
    [SerializeField] public int totalMoney = 0;     // everything earned this game, never goes down
 
    // study results: request ID -> methodology ID the player matched it with.
    // a list instead of a Dictionary, because JsonUtility and the Inspector can't handle dictionaries
    /// <summary>
    /// Request ID -> Methodology ID
    /// </summary>
    [SerializeField] private StudyResultList studyResults = new StudyResultList();
    public IReadOnlyList<StudyResult> StudyResults => studyResults.Results;
 
    // raised after money is added. the HUD listens to this to update its label
    
    /// <summary>
    /// Triggered when money is added. HUD listens to this to update
    /// </summary>
    public event Action<int> MoneyEarned;

    private GameSystems systems;

    //These are in the Research_Lab scene, so they're found after it loads instead of set in the Inspector
    
    //TODO: not sure if these should be added here or as components
    private ClientGenerator clientGenerator;
    private ClientSpawner clientSpawner;
    private ResearcherGenerator researcherGenerator;
    private ResearcherSpawner researcherSpawner;
    private readonly List<int> usedRequestIds = new List<int>();   //Requests already used

    private void Awake()
    {
        EditorSceneManager.playModeStartScene = null;
        systems = new GameSystems();
 
        // set the seed before any generator uses it (clients and researchers both draw from it)
        if (overrideSeed)
            GameSeed.Set(seedOverride);
        else
            GameSeed.EnsureInitialized();
        Debug.Log($"GameInitializer: game seed {GameSeed.Seed} (override {(overrideSeed ? "ON" : "OFF")})");
    }

    private async Awaitable Start()
    {
        var components = await systems.SceneLoader.AddScenes();
        //This is where persistent systems accessor is sent to the other scenes.
        //A scene without an ISceneInitializable comes back null, so check before calling.
        components.first?.InitializeScene(systems);
        components.second?.InitializeScene(systems);

        InitializeClients();
        InitializeResearchers();
    }
    
    /// <summary>
    /// Gives the client generator the requests from DataManager
    /// Also finds the generator and spawner
    /// </summary>
    private void InitializeClients()
    {
        clientGenerator = FindAnyObjectByType<ClientGenerator>();
        clientSpawner = FindAnyObjectByType<ClientSpawner>();

        if (clientGenerator == null || clientSpawner == null)
        {
            Debug.LogError("GameInitializer: no ClientGenerator / ClientSpawner found. Are they in the Research_Lab scene?");
            return;
        }

        clientGenerator.Initialize(systems.DataManager.RequestList.Requests);
        Debug.Log("Clients initialized");
    }

    // demo: sends in the next client from the deck. keeps going until the requests run out
    
    /// <summary>
    /// This will need to be updated in the future.
    /// Currently, this is set up for the demo to spawn in the next client from the deck
    /// </summary>
    public void SpawnNextClient()
    {
        if (clientGenerator == null || clientSpawner == null) return;

        List<ClientData> next = clientGenerator.GenerateDay(day, 0, usedRequestIds);
        if (next.Count == 0)
        {
            Debug.Log("No requests left to send in.");
            return;
        }

        usedRequestIds.Add(next[0].RequestId);
        clientSpawner.Spawn(next[0]);
    }
    
    // makes the pool of researchers (hired and unhired) from the game seed.
    // untick Generate On Start on the ResearcherGenerator, otherwise it makes a second pool on its own Start
    private void InitializeResearchers()
    {
        researcherGenerator = FindAnyObjectByType<ResearcherGenerator>();
        researcherSpawner = FindAnyObjectByType<ResearcherSpawner>();
 
        if (researcherGenerator == null || researcherSpawner == null)
        {
            Debug.LogError("GameInitializer: no ResearcherGenerator / ResearcherSpawner found. Are they in the Research_Lab scene?");
            return;
        }
 
        researcherGenerator.Generate();
        if (researcherSpawner.CubicleCount == 0)
            Debug.LogWarning("GameInitializer: the ResearcherSpawner has no Cubicle Spots, so nobody can be hired.");
 
        Debug.Log("Researchers initialized");
    }
 
    // hires a researcher into the first free cubicle. called by the HIRE buttons on the hire screen
    public Researcher HireResearcher(ResearcherData data)
    {
        if (researcherSpawner == null || data == null) return null;
 
        for (int cubicle = 1; cubicle <= researcherSpawner.CubicleCount; cubicle++)
        {
            if (researcherSpawner.IsCubicleFree(cubicle))
                return researcherSpawner.Hire(data, cubicle);
        }
 
        Debug.Log("No free cubicles left.");
        return null;
    }
    
    
    // called by the match screen after MATCH. adds the reward and remembers which method was used
    
    /// <summary>
    /// Called after Match. This will add the reward and stores the study result.
    ///Also invokes the money earned event
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <param name="methodologyId">Methodology ID</param>
    /// <param name="reward">Reward amount</param>
    public void RecordStudy(int requestId, int methodologyId, int reward)
    {
        currentMoney += reward;
        totalMoney += reward;
        StudyResult existing = studyResults.Results.Find(r => r.RequestId == requestId);
        if (existing != null) existing.MethodologyId = methodologyId;   // same request matched again
        else studyResults.Results.Add(new StudyResult { RequestId = requestId, MethodologyId = methodologyId });
        MoneyEarned?.Invoke(reward);
 
        Debug.Log($"Study recorded: request {requestId} -> methodology {methodologyId}, +${reward} " +
                  $"(current ${currentMoney}, total ${totalMoney})");
    }
    
    /// <summary>
    /// Converts study results to JSON, so it can be saved in DB
    /// </summary>
    /// <param name="prettyPrint"></param>
    /// <returns></returns>
    public string StudyResultsToJson(bool prettyPrint = false)
    {
        return JsonUtility.ToJson(studyResults, prettyPrint);
    }
    
    /// <summary>
    /// For loading study results from a save
    /// </summary>
    /// <param name="json"> JSON of study results</param>
    public void LoadStudyResults(string json)
    {
        studyResults = JsonUtility.FromJson<StudyResultList>(json) ?? new StudyResultList();
    }
 
    // methodology used for a request, or -1 if it hasn't been studied
    /// <summary>
    /// Gets the methodology used for a request, or -1 if it hasn't been studied
    /// </summary>
    /// <param name="requestId"> Request ID used to find methodology</param>
    /// <returns></returns>
    public int GetStudyMethodology(int requestId)
    {
        StudyResult result = studyResults.Results.Find(r => r.RequestId == requestId);
        return result != null ? result.MethodologyId : -1;
    }
}
