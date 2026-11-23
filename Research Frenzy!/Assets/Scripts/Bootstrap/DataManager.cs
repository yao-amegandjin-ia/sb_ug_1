using Bootstrap;
using Clients;
using Config;
using GameDataObjects;
using Interfaces;
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
    private List<ClientData> dailyClients = new List<ClientData>();

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
    
    /// <summary>
    /// True when there's a save to carry on: one was loaded at startup, or the game has been saved since.
    /// False after a new game deletes it, until the next save. The start screen shows CONTINUE when it's true.
    /// </summary>
    public bool HasSavedGame { get; private set; }

    /// <summary>Where saves go, for the Console and the data log ("browser localStorage", a folder, the API...).</summary>
    public string SaveLocation => saveSystem is LocalSaveSystem local ? local.Describe() : saveSystem.apiBaseUrl; 

    /// <summary>
    /// Includes request ids of study results and ids of any returning clients!
    /// </summary>
    /// <returns></returns>
    public List<int> GetUsedRequestIds()
    {
        //Return both the study results requests and the 
        var returningids = returningClients.Select(c => c.RequestId).ToList();
        returningids.AddRange(PlayerData.StudyResults.Keys.ToList());
        return returningids;
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
        instance.HasSavedGame = saveData.IsFromSave; 
        instance.BindData();
        return instance;
    }

    public void AddReturningClient(ClientData client)
    {
        if (ReturningClients.Any(id => client.Id == id.Id))
        {
            return; //Client already coming back tomorrow
        }
        state.Clients.Add(client);
    }

    /// <summary>
    /// Clears and updates the researchers from the new list. 
    /// </summary>
    /// <param name="researchers"></param>
    public void UpdateResearchers(List<ResearcherData> researchers)
    {
        state.Researchers.Clear();
        foreach(var r in researchers)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (Researchers.Any(re => re.Id == r.Id))
                throw new ArgumentException("Researcher ID already exists.", nameof(r));
            state.Researchers.Add(r);
        }
    }
    /// <summary>
    /// Clears and updates the daily clients from the new list. 
    /// </summary>
    /// <param name="clients"></param>
    public void UpdateDailyClients(List<ClientData> clients, bool includesReturning)
    {
        dailyClients.Clear();
        //Clear the returning clients state if they've been added to daily clients
        if (includesReturning)
        {
            state.Clients.Clear();
        }
        
        foreach(var c in clients)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (Clients.Any(cl => cl.Id == c.Id))
                throw new ArgumentException("Client ID already exists.", nameof(c));
            dailyClients.Add(c);
        }

    }

  
    public void ClearDailyClients()
    {
        dailyClients.Clear();
    }
    public void RemoveDailyClient(int index)
    {
        dailyClients.RemoveAt(index);
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
        HasSavedGame = true;  
        
    }

    /// <summary>
    /// Wipes the game back to a fresh day 1 with a new seed (NEW GAME on the start screen).
    /// Everything is cleared IN PLACE instead of making a new SaveData, because other scripts hold read-only views
    /// of these exact lists and dictionaries (ResearcherGenerator.Researchers, PlayerData...). A new object would
    /// leave them looking at the old game. Doesn't touch the save on disk: FirmController deletes that.
    /// </summary>
    public void StartNewGame(int seed)
    {
        state.Seed = seed;

        PlayerData p = state.Player;
        p.CurrentMoney = 0;
        p.TotalMoneyEarned = 0;
        p.CurrentDay = 1;
        p.StudyResults.Clear();
        p.StudyApproaches.Clear();
        p.OwnedUpgrades.Clear();
        p.MidGameQuizTaken = false;

        state.Researchers.Clear();   // FirmController generates a fresh pool next
        state.Clients.Clear();       // no one is returning tomorrow
        dailyClients.Clear();
    }

    /// <summary>Copies the UpgradeController's levels in, just before saving (upgrade id -> level).</summary>
    public void SetOwnedUpgrades(Dictionary<string, int> owned)
    {
        // in place, same reason as StartNewGame
        state.Player.OwnedUpgrades.Clear();
        if (owned == null) return;
        foreach (KeyValuePair<string, int> pair in owned)
            state.Player.OwnedUpgrades[pair.Key] = pair.Value;
    }

    /// <summary>The day 4 quiz was finished (or not). Saved with the game.</summary>
    public void SetMidGameQuizTaken(bool taken)
    {
        state.Player.MidGameQuizTaken = taken;
    }

    /// <summary>Deletes the save. There's nothing to CONTINUE afterwards.</summary>
    public async Awaitable DeleteSave()
    {
        HasSavedGame = false;
        await saveSystem.DeleteSave();
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
    // TEMP REPEATS : payAgain is new. True = pay even if this request was answered before (a repeat)
    public bool TryRecordStudy(int requestId, ResearchApproach approach, int methodologyId, int reward, bool payAgain = false)
    {
        if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
        if (methodologyId < 0) throw new ArgumentOutOfRangeException(nameof(methodologyId));

        // the approach is saved whether or not this pays, the same as the methodology below
        state.Player.StudyApproaches[requestId] = (int)approach;

        // -1 is a reserved/unanswered request, not a final answer.
        if (!payAgain && state.Player.StudyResults.TryGetValue(requestId, out int method) && method != -1)
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
    public bool MidGameQuizTaken => player.MidGameQuizTaken;   // SAVE GAME (new)
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