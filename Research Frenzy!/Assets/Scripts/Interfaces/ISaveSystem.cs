using Assets.Scripts.Bootstrap;
using Assets.Scripts.GameDataObjects;
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
        /// 
        public Awaitable SaveGame(SaveData saveData);

        /// <summary>
        /// Asynchronous method to load game data from the database. 
        /// </summary>
        /// <returns></returns>
        public Awaitable<SaveData> LoadGame();
        /// <summary>
        /// Sends the final player state to the database for data collection purposes and cleans unneeded data. 
        /// </summary>
        /// <param name="finalPlayerState"></param>
        /// <returns></returns>
        public Awaitable EndGame(PlayerData finalPlayerState);
        /// <summary>
        /// Saves the data from multiple choice quiz. 
        /// </summary>
        /// <param name="quizType"> Middle or Postgame</param>
        /// <param name="responses">{questionId: response}</param>
        /// <returns></returns>
        public Awaitable SaveQuizData(QuizType quizType, Dictionary<int, int> responses);
        

        


    }
    public enum QuizType
    {
        MidGame,
        PostGame
    }
}
