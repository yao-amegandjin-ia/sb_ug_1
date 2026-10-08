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

    public RequestList RequestList { get; private set; } 


    public DataManager() 
    {
        RequestList = new RequestList();
        InitializeDataObjects();
    }


 


    
    // Could make a create a service to have data objects register for the DataManager but seems like adding abstraction
    //Could also make an interface service and a delegate or callback registering with the manager
    private void InitializeDataObjects()
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

