using Assets.Scripts.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;



/// <summary>
/// This class handles the creation of data objects on game startup.
/// </summary>
public class DataManager
{


    public static DataManager Instance { get; private set; } = new DataManager();

    public RequestList RequestList { get; private set; } 


    private DataManager() 
    {
        RequestList = new RequestList();
    }


    //This tells unity to run this method before any scene is loaded, so on game startup. 

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        //If debugger not attached yet wait
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }
        Instance.InitializeDataObjects();
    }


    
    // Could make a create a service to have data objects register for the DataManager but seems like adding abstraction
    //Could also make an interface service and a delegate or callback registering with the manager
    public void InitializeDataObjects()
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

