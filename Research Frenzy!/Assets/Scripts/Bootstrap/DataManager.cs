using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
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

    public RequestList RequestList { get; private set; }

    public SaveData SaveData { get; private set; }

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

    
    

    /// <summary>
    /// Sets DataManager's SaveData and returns it. 
    /// Note SaveData may have empty fields after a load meaning first load for player.
    /// </summary>
    /// <returns></returns>
    public async Awaitable<SaveData> Load()
    {
        SaveData saveData = await _saveSystem.LoadGame();
        SaveData = saveData;
        return saveData;
    }

    /// <summary>
    /// Saves the DataManager's SaveData object.
    /// </summary>
    /// <returns></returns>
    public async Awaitable SaveGame()
    {
        await _saveSystem.SaveGame(SaveData);
    }

    
/// <summary>
/// Sets researcher savedata based off of ResearcherData. 
/// </summary>
/// <param name="researchers"></param>
/// <exception cref="ArgumentNullException"></exception>
/// <exception cref="InvalidOperationException"></exception>
public async Awaitable SetResearchers(IEnumerable<ResearcherData> researchers)
{
    if (researchers == null)
        throw new ArgumentNullException(nameof(researchers));

    if (SaveData == null)
        throw new InvalidOperationException(
            "Load or initialize SaveData before setting researchers.");
    if ()
    

    var savedResearchers = new List<ResearcherDataObject>();

    foreach (ResearcherData researcher in researchers)
    {
        var methodologies = new List<MethodologiesEnum>();

        foreach (int id in researcher.KnownMethodologyIds)
        {
            methodologies.Add((MethodologiesEnum)id);
        }

        savedResearchers.Add(new ResearcherDataObject
        {
            ResearcherId = researcher.Id,
            ClientId = researcher.CurrentClientId,
            Name = researcher.Name,
            Postion = researcher.Position,
            Experience = researcher.Experience,
            Speed = researcher.Speed,
            Quality = researcher.Quality,
            Methodologies = methodologies,
            Skin = new List<int>
            {
                researcher.Appearance.x, // Head
                researcher.Appearance.y, // Torso
                researcher.Appearance.z  // Legs
            }
        });
    }

    SaveData.Researchers = savedResearchers;
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

