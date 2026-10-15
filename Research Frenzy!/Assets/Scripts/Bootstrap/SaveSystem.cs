using Assets.Scripts.GameObjects;
using Assets.Scripts.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Assets.Scripts.Bootstrap
{
    /// <summary>
    /// Implement this base class to create the new adapter for the cyclone commons api!
    /// </summary>
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
        
       
        //For now just calling createOrResume endpoint on every load. I presume load will only be called on game start up. 
        public override async Awaitable<SaveData> LoadGame()
        {
            await CheckServerHealth();

            //Presumably for cyclone commons the playerid will be super easy to grab rightttt?

            string responseString = await CreateOrResume(GameInitializer.playerId);
            JObject response = JObject.Parse(responseString);

            string sessionId = response["session_id"].Value<string>();
            string playerId = response["player_uuid"].Value<string>();

            string endpointURL = apiBaseUrl + $"/sessions/{sessionId}/save";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(endpointURL))
            {
                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception(
                        $"{webRequest.responseCode}: {webRequest.error}"
                    );
                }

                JObject loadResponse = JObject.Parse(
                    webRequest.downloadHandler.text
                );

                JObject player = (JObject)loadResponse["player"];

                return new SaveData
                {
                    PlayerId = playerId,
                    SessionId = sessionId,

                    CurrentMoney = player["current_money"].Value<int>(),
                    TotalMoneyEarned = player["total_money_earned"].Value<int>(),
                    CurrentDay = player["current_day"].Value<int>(),

                    StudyResults = player["study_results"]
                        .ToObject<Dictionary<int, MethodologiesEnum>>(),

                    OwnedUpgrades = player["owned_upgrades"]
                        .ToObject<Dictionary<string, int>>(),

                    Researchers = loadResponse["researchers"]
                        .ToObject<List<ResearcherSaveData>>(),

                    Clients = loadResponse["clients"]
                        .ToObject<List<ClientSaveData>>()
                };
            }
        }

        public override async Awaitable<bool> SaveGame(SaveData saveData)
        {
            await CheckServerHealth();
            
            throw new NotImplementedException();
        }


        private async Awaitable<string> CreateOrResume(string playerId)
        {
            string endpointURL = apiBaseUrl + "/sessions";
            string body = JsonConvert.ToString(
                new
                {
                    player_uuid = playerId
                }
                );
            
            using (UnityWebRequest webRequest = UnityWebRequest.Post(apiBaseUrl, body, "application/json"))
            {
                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"{webRequest.responseCode}: {webRequest.error}");
                }

                return webRequest.downloadHandler.text;


            }
        }

       //Returns true if the server's status is good. 
        private async Awaitable CheckServerHealth()
        {
            string endpointURL = apiBaseUrl+ "/health";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(endpointURL))
            {
                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"{webRequest.responseCode}: {webRequest.error}");
                }

                


            }
        }
    }
}
