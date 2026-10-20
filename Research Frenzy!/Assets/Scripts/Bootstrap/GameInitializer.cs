using Assets.Scripts.Bootstrap;
using UnityEngine;
using Assets.Scripts.Interfaces;
using System.Collections.Generic;
using System;
using Clients;

/// <summary>
/// Runs at the start of the game
/// Acts as a controller for the game
/// </summary>

public class GameInitializer : MonoBehaviour
{
    public int day = 1;   // placeholder until the day system exists

    private GameSystems systems;

    //These are in the Research_Lab scene, so they're found after it loads instead of set in the Inspector
    private ClientGenerator clientGenerator;
    private ClientSpawner clientSpawner;
    private readonly List<int> usedRequestIds = new List<int>();   //Requests already used

    private void Awake()
    {
        systems = new GameSystems();
    }

    private async Awaitable Start()
    {
        var components = await systems.SceneLoader.AddScenes();
        //This is where persistent systems accessor is sent to the other scenes.
        //A scene without an ISceneInitializable comes back null, so check before calling.
        components.first?.InitializeScene(systems);
        components.second?.InitializeScene(systems);

        InitializeClients();
    }
    
    /// <summary>
    /// Gives the client generator the requests from DataManager
    /// Also finds the generator and spawner
    /// </summary>
    private void InitializeClients()
    {
        clientGenerator = FindAnyObjectByType<ClientGenerator>();
        clientSpawner = FindAnyObjectByType<ClientSpawner>();

        if (clientGenerator == null || clientSpawner == null)
        {
            Debug.LogError("GameInitializer: no ClientGenerator / ClientSpawner found. Are they in the Research_Lab scene?");
            return;
        }

        clientGenerator.Initialize(systems.DataManager.RequestList.Requests);
        Debug.Log("Clients initialized");
    }

    // demo: sends in the next client from the deck. keeps going until the requests run out
    
    /// <summary>
    /// This will need to be updated in the future.
    /// Currently, this is set up for the demo to spawn in the next client from the deck
    /// </summary>
    public void SpawnNextClient()
    {
        if (clientGenerator == null || clientSpawner == null) return;

        List<ClientData> next = clientGenerator.GenerateDay(day, 0, usedRequestIds);
        if (next.Count == 0)
        {
            Debug.Log("No requests left to send in.");
            return;
        }

        usedRequestIds.Add(next[0].RequestId);
        clientSpawner.Spawn(next[0]);
    }
}
