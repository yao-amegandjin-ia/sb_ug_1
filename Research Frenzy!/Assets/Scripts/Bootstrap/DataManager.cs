﻿using Bootstrap;
using GameDataObjects;
using Interfaces;
using Clients;
using Config;
using Researchers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;



/// <summary>
/// This class handles the creation of data objects on game startup.
/// </summary>
public class DataManager
{
    public int Seed => state.Seed;
    private SaveData state = new SaveData();
    private readonly IReadOnlyList<ClientData> dailyClients = new List<ClientData>();

    private  IReadOnlyList<ResearcherData> researchers;

    private IReadOnlyList<ClientData> returningClients;

    private readonly RequestList Requests;

    public event Action<int> MoneyEarned;
    public event Action<int> MoneySpent;

    public PlayerDataReadonly PlayerData { get; private set; }
    
    //Arrows make so cannot change reference!
    public IReadOnlyList<ResearcherData> Researchers => researchers;
    public IReadOnlyList<ClientData> Clients => dailyClients;
    public IReadOnlyList<ClientData> ReturningClients => returningClients;

    private readonly BaseSaveSystem saveSystem;

    public List<int> GetUsedRequestIds()
    {
        return PlayerData.StudyResults.Keys.ToList();
    }
    
    
    private DataManager(BaseSaveSystem saveSystem)
    {
        
        this.saveSystem = saveSystem;
        Requests = InitializeRequestData();   // the config spreadsheets, packed in the build, so this is instant
        
    }

    private void BindData()
    {
        PlayerData = new PlayerDataReadonly(state.Player);
        researchers = state.Researchers.AsReadOnly();
        returningClients = state.Clients.AsReadOnly();

    }
    public static async Awaitable<DataManager> CreateAsync(BaseSaveSystem saveSystem)
    {
        var instance = new DataManager(saveSystem);
        var saveData = await saveSystem.LoadGame();
        if (saveData == null) throw new InvalidOperationException("Save system returned null state.");

        if (saveData.Player.CurrentDay < 1)
        {
            saveData.Player.CurrentDay = 1;
        }

        instance.state = saveData;
        instance.BindData();
        return instance;
    }

/// <summary>
/// Clears and updates the researchers from the new list. 
/// </summary>
/// <param name="researchers"></param>
  public void UpdateResearchers(List<ResearcherData> researchers)
    {
        researchers.Clear();
        foreach(var r in researchers)
        {
            AddResearcher(r);
        }
    }
    /// <summary>
    /// Clears and updates the daily clients from the new list. 
    /// </summary>
    /// <param name="clients"></param>
    public void UpdateDailyClients(List<ClientData> clients)
    {

    }

    private void AddResearcher(ResearcherData researcher)
    {
        if (researcher == null) throw new ArgumentNullException(nameof(researcher));
        if (state.Researchers.Any(r => r.Id == researcher.Id))
            throw new ArgumentException("Researcher ID already exists.", nameof(researcher));
        state.Researchers.Add(researcher);

    }
    public void AdvanceDay()
    {
        state.Player.CurrentDay = checked(state.Player.CurrentDay + 1);
    }




    /// <summary>
    /// Saves the DataManager's SaveData object.
    /// </summary>
    /// <returns></returns>
    public async Awaitable SaveGame()
    {
        
        await saveSystem.SaveGame(state);
        
    }

    



    public IReadOnlyList<RequestData> GetRequests() => Requests.Requests;


    public void DecreaseMoney(int byValue)
    {
        state.Player.CurrentMoney -= byValue;
    }

    // money that isn't from a study, like the quiz rewards. counts toward the total (the score) too
    public void IncreaseMoney(int byValue)
    {
        if (byValue < 0) throw new ArgumentOutOfRangeException(nameof(byValue));
        state.Player.CurrentMoney = checked(state.Player.CurrentMoney + byValue);
        state.Player.TotalMoneyEarned = checked(state.Player.TotalMoneyEarned + byValue);
        MoneyEarned?.Invoke(byValue);
    }

    /// <summary>
    /// Saves the player's answer for a request: the methodology (step two) and the approach (step one),
    /// and pays the reward the first time the request is answered.
    /// </summary>
    public bool TryRecordStudy(int requestId, ResearchApproach approach, int methodologyId, int reward)
    {
        if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
        if (methodologyId < 0) throw new ArgumentOutOfRangeException(nameof(methodologyId));

        // the approach is saved whether or not this pays, the same as the methodology below
        state.Player.StudyApproaches[requestId] = (int)approach;

        // -1 is a reserved/unanswered request, not a final answer.
        if (state.Player.StudyResults.TryGetValue(requestId, out int method) && method != -1)
        {
            // Keep the latest choice without paying the same request twice.
            state.Player.StudyResults[requestId] = methodologyId;
            return false;
        }
        int money = checked(state.Player.CurrentMoney + reward);
        int total = checked(state.Player.TotalMoneyEarned + reward);
        state.Player.StudyResults[requestId] = methodologyId;
        state.Player.CurrentMoney = money;
        state.Player.TotalMoneyEarned = total;
        state.Clients.RemoveAll(c => c.RequestId == requestId);
        MoneyEarned?.Invoke(reward);
        return true;
    }





    /// <summary>Everything that was wrong in the config spreadsheets this time (empty if all good).</summary>
    public ConfigReport ConfigReport { get; private set; }

    /// <summary>
    /// Reads the config spreadsheets in Assets/Resources/Config:
    /// methodologies.csv first (requests name their methods, so the names must be known), then requests.csv.
    /// </summary>
    private RequestList InitializeRequestData()
    {
        ConfigReport = new ConfigReport();
        var requests = new RequestList();

        // 1. methodologies (MethodLibrary keeps them for the whole game)
        MethodLibrary.LoadFromConfigFolder(ConfigReport);

        // 2. requests, with method names turned into methodology IDs
        string requestsText = ConfigFiles.Read(RequestConfig.FileName, ConfigReport);
        if (requestsText != null)
            requests.Requests = RequestConfig.Parse(requestsText, MethodLibrary.GetAll(), ConfigReport);

        Debug.Log($"DataManager: loaded {MethodLibrary.GetAll().Count} methodologies and {requests.Requests.Count} requests " +
                  $"from {ConfigFiles.Folder}");
        ConfigFiles.Log(ConfigReport);   // one warning listing every problem, with file and row
        return requests;
    }

}

/// <summary>Live read-only player data. </summary>
public sealed class PlayerDataReadonly
{
    private readonly PlayerData player;
    public int CurrentMoney => player.CurrentMoney;
    public int TotalMoneyEarned => player.TotalMoneyEarned;
    public int CurrentDay => player.CurrentDay;
    public IReadOnlyDictionary<int, int> StudyResults { get; }
    public IReadOnlyDictionary<int, int> StudyApproaches { get; }
    public IReadOnlyDictionary<string, int> OwnedUpgrades { get; }

    internal PlayerDataReadonly(PlayerData player)
    {
        this.player = player;
        StudyResults = new ReadOnlyDictionary<int, int>(player.StudyResults);
        StudyApproaches = new ReadOnlyDictionary<int, int>(player.StudyApproaches);
        OwnedUpgrades = new ReadOnlyDictionary<string, int>(player.OwnedUpgrades);
    }
}

