using Assets.Scripts.Bootstrap;
using UnityEngine;
using Assets.Scripts.Interfaces;
using System.Collections.Generic;
using System;
public class GameInitializer : MonoBehaviour
{


    //For testing this is going to be referenced in the savesystem.
    public static readonly string playerId = "a1b2c3d4-0000-4000-8000-000000000001";
    
    private GameSystems systems;

    private void Awake()
    {
        systems = new GameSystems();
        
    }

    private async Awaitable Start()
    {
        
        var components = await systems.SceneLoader.AddScenes();
        //This is where persistent systems accessor is sent to the other scenes. 
        components.first.InitializeScene(systems);
        components.second.InitializeScene(systems);

    }

}
