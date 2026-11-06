using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using Clients;
using Researchers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;



/// <summary>
/// This class handles the creation of data objects on game startup.
/// </summary>
public class DataManager
{
    public int Seed => _saveData.Seed ;
    private SaveData _saveData = new SaveData();

    public RequestList RequestList { get; private set; }

    public PlayerData PlayerData => _saveData.Player;

    public IReadOnlyList<ResearcherData> Researchers =>
        _saveData.Researchers;

    public IReadOnlyList<ClientData> Clients =>
        _saveData.Clients;

    // Examples of modifications inside DataManager:
    public void AddResearcher(ResearcherData researcher)
    {
        _saveData.Researchers.Add(researcher);
    }

    public void AddClient(ClientData client)
    {
        _saveData.Clients.Add(client);
    }

    private readonly BaseSaveSystem _saveSystem;


    private DataManager(BaseSaveSystem saveSystem)
    {
        RequestList = new RequestList();
        _saveSystem = saveSystem;
        InitializeRequestData();
    }

    public static async Awaitable<DataManager> CreateAsync(BaseSaveSystem saveSystem)
    {
        var instance = new DataManager(saveSystem);
        //perform async work
        var saveData = await instance.Load();
        return instance;
    }

    public List<ResearcherData> GetResearchers()
    {
        return _saveData.Researchers;
    }
    

    /// <summary>
    /// Sets DataManager's SaveData and returns it. 
    /// Note SaveData may have empty fields after a load meaning first load for player.
    /// </summary>
    /// <returns></returns>
    public async Awaitable<SaveData> Load()
    {
        SaveData saveData = await _saveSystem.LoadGame();
        
        //If on day one there should be no game data loaded!
        if (PlayerData.CurrentDay == 1)
        {
            saveData.Clients.Clear();
            saveData.Researchers.Clear();
            saveData.Player.CurrentMoney = 0;
            saveData.Player.OwnedUpgrades.Clear();
            saveData.Player.StudyResults.Clear();
            saveData.Player.TotalMoneyEarned = 0;
        }
        //Clear out for regeneration if not the proper amount of researchers!
        if (Researchers.Count != 10)
        {
            saveData.Researchers.Clear();
        }


        
        _saveData = saveData;
        return saveData;
    }

    /// <summary>
    /// Saves the DataManager's SaveData object.
    /// </summary>
    /// <returns></returns>
    public async Awaitable SaveGame()
    {
        await _saveSystem.SaveGame(_saveData);
    }

    
/// <summary>
/// Sets researcher savedata based off of ResearcherData. 
/// </summary>
/// <param name="researchers"></param>
/// <exception cref="ArgumentNullException"></exception>
/// <exception cref="InvalidOperationException"></exception>
public void SetResearchers(List<ResearcherData> researchers)
{
    if (researchers == null)
        throw new ArgumentNullException(nameof(researchers));

    if (_saveData == null)
        throw new InvalidOperationException(
            "Load or initialize SaveData before setting researchers.");

        _saveData.Researchers = researchers;
    
}



    public RequestData GetRequest(int index)
    {
        return RequestList.Requests[index];
    }
    
    

    
    
    private void InitializeRequestData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Config", "requests.json");
        filePath = filePath.Replace('\\', '/');

        if (File.Exists(filePath))
        {
            string jsonString = File.ReadAllText(filePath);
            RequestList = JsonUtility.FromJson<RequestList>(jsonString);
        }
        else
        {
            Debug.Log($"JSON file failed to load/not found. {filePath}");
        }






    }

}

