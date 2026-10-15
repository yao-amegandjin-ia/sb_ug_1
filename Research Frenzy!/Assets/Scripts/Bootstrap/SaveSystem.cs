using Assets.Scripts.GameObjects;
using Assets.Scripts.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Scripts.Bootstrap
{

    public abstract class BaseSaveSystem : ISaveSystem
    {
        public readonly string apiBaseUrl;
        public BaseSaveSystem(string apiBaseUrl)
        {
            this.apiBaseUrl = apiBaseUrl;
        }
        public abstract Awaitable<SaveData> LoadGame();

        public abstract Awaitable<bool> SaveGame(SaveData saveData);
    }
    /// <summary>
    /// This is the concrete adapter implementation for the SaveSystem. Implement a seperate concrete class and inject the new dependency to switch this out. 
    /// </summary>
    public class SaveSystem : BaseSaveSystem
    {

      

        public SaveSystem(string apiBaseUrl) : base(apiBaseUrl) { }
        
        public override async Task<Awaitable<SaveData>> LoadGame()
        {
            string endpointURL = "/sessions/{}"
            using (UnityWebRequest webRequest = UnityWebRequest.Get())



                throw new NotImplementedException();
        }

        public override async Awaitable<bool> SaveGame(SaveData saveData)
        {
            throw new NotImplementedException();
        }


       //Returns true if the server's status is good. 
        private async Awaitable<bool> CheckServerHealth()
        {
            string endpointURL = apiBaseUrl+ "/health";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(endpointURL))
            {
                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception(webRequest.error);
                }

                return true;


            }
        }
    }
}
