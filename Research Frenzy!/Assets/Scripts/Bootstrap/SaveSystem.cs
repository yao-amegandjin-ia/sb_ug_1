using Assets.Scripts.GameDataObjects;
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
using System.Runtime.CompilerServices;
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

        public abstract Awaitable SaveGame(SaveData saveData);
    }
    /// <summary>
    /// This is the concrete adapter implementation for the SaveSystem. Implement a seperate concrete class and inject the new dependency to switch this out. 
    /// </summary>
    public class SaveSystem : BaseSaveSystem
    {
        //Populated by the Cyclones commons API very easily!
        //
        private string _playerId;
        private string _sessionId;
        private DateTime _lastPlayed;

        //Inject a test playerId!
        public SaveSystem(string apiBaseUrl, string playerId) : base(apiBaseUrl) 
        {
            _playerId = playerId;
        }
        
       
        //For now just calling createOrResume endpoint on every load. I presume load will only be called on game start up. 
        public override async Awaitable<SaveData> LoadGame()
        {
            await CheckServerHealth();

            //In the cyclones commons adapter I assume getttingPlayerId will be super easy. They would then store in the static global var.

            string responseString = await CreateOrResume(_playerId);
            JObject response = JObject.Parse(responseString);

             _sessionId = response["session_id"].Value<string>();
            _playerId = response["player_uuid"].Value<string>();

            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/save";

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

                    Player = loadResponse["player"].ToObject<PlayerData>(),

                    Researchers = loadResponse["researchers"]
                        .ToObject<List<ResearcherData>>(),

                    Clients = loadResponse["clients"]
                        .ToObject<List<ClientData>>()
                };
            }
        }

        public override async Awaitable SaveGame(SaveData saveData)
        {
            await CheckServerHealth();
            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/save";

            string body = JsonConvert.SerializeObject(
                new
                {
                    player = saveData.Player,
                    researchers = saveData.Researchers,
                    clients = saveData.Clients
                }
                );
            
            using (UnityWebRequest webRequest = UnityWebRequest.Put(endpointURL, body))
            {
                webRequest.SetRequestHeader("Content-Type", "application/json");

                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"{webRequest.responseCode}: {webRequest.error}");
                }
               
            }

        }


        private async Awaitable<string> CreateOrResume(string playerId)
        {
            string endpointURL = apiBaseUrl + "/sessions";
            string body = JsonConvert.SerializeObject(new
            {
                player_uuid = playerId
            });

            using (UnityWebRequest webRequest = UnityWebRequest.Post(endpointURL, body, "application/json"))
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
