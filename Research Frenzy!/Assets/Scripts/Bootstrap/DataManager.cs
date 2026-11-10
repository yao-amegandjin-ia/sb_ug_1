using Assets.Scripts.Bootstrap;
using Clients;
using Researchers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using GameDataObjects;
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
        Requests = InitializeRequestData();
        
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


  public void SetResearchers(List<ResearcherData> researchers)
    {
        this.researchers = researchers;
    }

    public void AddResearcher(ResearcherData researcher)
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

    public bool TryRecordStudy(int requestId, int methodologyId, int reward)
    {
        if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
        if (methodologyId < 0) throw new ArgumentOutOfRangeException(nameof(methodologyId));
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





    private RequestList InitializeRequestData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Config", "requests.json");
        filePath = filePath.Replace('\\', '/');
        var requests = new RequestList();
        if (File.Exists(filePath))
        {
            string jsonString = File.ReadAllText(filePath);
            requests = JsonUtility.FromJson<RequestList>(jsonString);
            return requests;
        }
        else
        {
            Debug.Log($"JSON file failed to load/not found. {filePath}");
            return requests;
        }


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
    public IReadOnlyDictionary<string, int> OwnedUpgrades { get; }

    internal PlayerDataReadonly(PlayerData player)
    {
        this.player = player;
        StudyResults = new ReadOnlyDictionary<int, int>(player.StudyResults);
        OwnedUpgrades = new ReadOnlyDictionary<string, int>(player.OwnedUpgrades);
    }
}

