using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameObjects;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;


namespace Assets.Scripts.Interfaces
{
    /// <summary>
    /// This interface defines the save system so it can function even upon switching the API. 
    /// </summary>
    public interface ISaveSystem
    {

        /// <summary>
        /// Asynchronous method to save game data in the database. 
        /// </summary>
        /// <param name="saveData"></param>
        /// <returns></returns>
        public Awaitable<bool> SaveGame(SaveData saveData);

        /// <summary>
        /// Asynchronous method to load game data from the database. 
        /// </summary>
        /// <returns></returns>
        public Awaitable<SaveData> LoadGame();
        

        


    }
}
