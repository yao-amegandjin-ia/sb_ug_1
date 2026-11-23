using Bootstrap;
using GameDataObjects;
using Interfaces;
using Clients;
using GameLogic;
using GameLogic.Upgrades;
using Researchers;
using System;
using System.Collections.Generic;
using UnityEngine;
using DataLogging;
using System.IO;


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
    public enum SaveBackend
    {
        Local,   // a JSON file in the Editor / desktop, the browser's localStorage in WebGL (LocalSaveSystem)
        Api      // the save API (SaveSystem). Falls back to Local if it can't be reached
    }

    [Header("Saving")]
    [Tooltip("Local: a JSON file (Editor: Assets/LocalSaves/save.json) or, in WebGL, the browser's localStorage. " +
             "Api: the save server at the URL in this script, falling back to Local if it's not running.")]
    [SerializeField] private SaveBackend saveBackend = SaveBackend.Local;

    public int day = 1;   // placeholder until the day system exists
    
    [Header("Seed")]
    [Tooltip("If ticked, uses the seed below as the game seed. Otherwise a random seed is made at startup.")]
    [SerializeField] private bool overrideSeed = false;
    [SerializeField] private int seedOverride = 12345;
    

    private GameSystems systems;
    
    private readonly string _apiUrl = "http://localhost:8080";
    private string saveSource = "local";   // "api", or "local" (SAVE GAME: was "offline"). Only used by the data log
    //Going to get overwritten anyway

    private void Awake()
    {

#if UNITY_EDITOR
        // Editor-only code. UnityEditor doesn't exist in a WebGL (or any) build, so without the #if the build fails
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
#endif
        
        
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

    // Unity runs this when the Bootstrap scene starts. The lab and UI scenes are only added at the end, after the
    // save has loaded, so if anything here fails, nothing else appears. The try/catch makes sure the reason
    // shows in the Console as a red error instead of the game silently stopping.
    private async Awaitable Start()
    {
        try
        {
            string _playerId = "e0a63563-3f66-4407-b6b2-6529f9a17e57";
            DataManager dataManager = await CreateDataManager(_playerId);
            systems = new GameSystems(dataManager);
            //Set with seed grabbed from session creation
            GameSeed.Set(dataManager.Seed);

            // DATA LOG: starts this session's log file and logs session_start + save_loaded.
            // Has to come before the scenes load, so the first client is logged too
            GameLog.StartSession(new SessionInfo
            {
                Seed = dataManager.Seed,
                SaveSource = saveSource,
                Day = dataManager.PlayerData.CurrentDay,
                Money = dataManager.PlayerData.CurrentMoney,
                TotalMoneyEarned = dataManager.PlayerData.TotalMoneyEarned,
                RequestsAnswered = dataManager.PlayerData.StudyResults.Count,
                UpgradesOwned = dataManager.PlayerData.OwnedUpgrades.Count,
                ResearchersHired = dataManager.Researchers?.Count ?? 0,
            });
            
            Debug.Log("GameInitializer: loading the Research_Lab and UI scenes");
            var components = await systems.SceneLoader.AddScenes();

            //This is where persistent systems accessor is sent to the other scenes.
            //A scene without an ISceneInitializable comes back null, so check before calling.
            if (components.first == null) Debug.LogError("GameInitializer: no ISceneInitializable (FirmController) found in Research_Lab.");
            if (components.second == null) Debug.LogError("GameInitializer: no ISceneInitializable (UIInitializer) found in UI.");
            components.first?.InitializeScene(systems);
            components.second?.InitializeScene(systems);

            //Extra initialization
            if (components.first is FirmController firm && components.second is UIInitializer ui)
                ui.SendFirm(firm);

            Debug.Log("GameInitializer: startup finished");
        }
        catch (Exception e)
        {
            // LogException prints the full error and the line it came from, in red
            Debug.LogError("GameInitializer: startup stopped because of the error below.");
            Debug.LogException(e);
        }
    }

    // picks the save system from Save Backend. It used to always try the API first and fall back to
    // JsonFileSaveSystem, which writes files, so it couldn't work in WebGL. LocalSaveSystem works in both.
    // If there's no save yet, the save system hands back a fresh day 1 and the start screen has no CONTINUE.
    private async Awaitable<DataManager> CreateDataManager(string playerId)
    {
        int seed = GameSeed.EnsureInitialized();

        if (saveBackend == SaveBackend.Api)
        {
            try
            {
                saveSource = "api";
                return await DataManager.CreateAsync(new SaveSystem(_apiUrl, playerId, seed));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameInitializer: couldn't load the save from {_apiUrl} ({e.Message.Trim()}). " +
                                 "Saving on this device instead.");
            }
        }

        saveSource = "local";
        return await DataManager.CreateAsync(new LocalSaveSystem(seed, LocalSaveSystem.StorageForThisPlatform()));
    }
}
