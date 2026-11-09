using Assets;
using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using Clients;
using Codice.Client.BaseCommands.LayoutFilters;
using GameLogic;
using GameLogic.Upgrades;
using Researchers;
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Codice.Client.Common.EventTracking.TrackFeatureUseEvent.Features;


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
 
    /*
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
    
    /// <summary>
    /// Triggered when money is spent.
    /// </summary>
    public event Action<int> MoneySpent;
    */

    private GameSystems systems;

    

    
    private readonly string _apiUrl = "http://localhost:8080";
    //Going to get overwritten anyway

    private void Awake()
    {

        EditorSceneManager.playModeStartScene = null;
        
        
        //if (upgrades == null) Debug.LogWarning("GameInitializer: no UpgradeController on this object, so upgrades won't do anything.");
        //Remember to fix upgradeController
        
        // set the seed before any generator uses it (clients and researchers both draw from it)
        if (overrideSeed)
            GameSeed.Set(seedOverride);
        else
            GameSeed.EnsureInitialized();
        Debug.Log($"GameInitializer: game seed {GameSeed.Seed} (override {(overrideSeed ? "ON" : "OFF")})");
        //Note: Do not move this above the GameSeed.Set
    }

    private async Awaitable Start()
    {
        string _playerId = "e0a63563-3f66-4407-b6b2-6529f9a17e56";
        //Create the data manager
        var dataManager = await DataManager.CreateAsync(new SaveSystem(_apiUrl, _playerId, GameSeed.EnsureInitialized()));
        systems = new GameSystems(dataManager);
        //Set with seed grabbed from session creation
        GameSeed.Set(dataManager.Seed);

        var components = await systems.SceneLoader.AddScenes();
        //This is where persistent systems accessor is sent to the other scenes.
        //A scene without an ISceneInitializable comes back null, so check before calling.
        try
        {
            components.first?.InitializeScene(systems);
            components.second?.InitializeScene(systems);
        }
        catch(Exception e)
        {
            Debug.Log(e.Message);
        }
        


        //Extra initialization
        if (components.first is FirmController firm &&
        components.second is UIInitializer ui)
        {
            ui.SendFirm(firm);
            
        }

    }
    
 


}
