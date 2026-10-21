using Assets;
using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using System;
using System.Collections.Generic;
using Unity.Loading;
using UnityEditor.SceneManagement;
using UnityEngine;
public class GameInitializer : MonoBehaviour
{


    
    
    private GameSystems systems;

    private void Awake()
    {
        EditorSceneManager.playModeStartScene = null;
        systems = new GameSystems();
        
    }

    private async Awaitable Start()
    {
        //var data = await LoadData();

        var components = await systems.SceneLoader.AddScenes();
        //This is where persistent systems accessor is sent to the other scenes. 
        components.first.InitializeScene(systems);
        components.second.InitializeScene(systems);

    }

    private async Awaitable<SaveData> LoadData()
    {
        SaveData saveData = await systems.SaveSystem.LoadGame();
        //systems.DataManager
        string hello = "hello";
        hello += 1;
        return saveData;

    }

    

}
