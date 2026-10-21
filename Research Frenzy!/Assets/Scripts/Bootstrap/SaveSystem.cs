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
using Assets;
using System.Runtime.CompilerServices;
using TMPro.EditorUtilities;
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

        public abstract Awaitable EndGame(PlayerData finalPlayerState);
        public abstract Awaitable<SaveData> LoadGame();

        public abstract Awaitable SaveGame(SaveData saveData);
        public abstract Awaitable SaveQuizData(QuizType quizType, Dictionary<int, int> responses);
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

        private int _seed;
        private DateTime _lastPlayed;

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="apiBaseUrl"></param>
        /// <param name="playerId">Set to NULL if no playerid</param>
        /// <param name="seed">Set to NUll if no seed</param>
        public SaveSystem(string apiBaseUrl, string playerId, int seed) : base(apiBaseUrl) 
        {
            _playerId = playerId;
            _seed = seed;
        }
        
       
        //For now just calling createOrResume endpoint on every load. I presume load will only be called on game start up. 
        public override async Awaitable<SaveData> LoadGame()
        {
            await CheckServerHealth();

            //Note if the body for create or resume is empty/filled with no data a seed and player id will be created. 
            string responseString = await CreateOrResume(_playerId, _seed);
            JObject response = JObject.Parse(responseString);

            //Update all of the database specific fields based upon what session create or resume returns. 
             _sessionId = response["session_id"].Value<string>();
            _playerId = response["player_uuid"].Value<string>();
            _lastPlayed = response["last_played"].Value<DateTime>();
            _seed = response["seed"].Value<int>();
            //We set the seed in the static class as well so that it is not mismatched!
            GameSeed.Set(_seed);

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
                        .ToObject<List<ResearcherDataObject>>(),

                    Clients = loadResponse["clients"]
                        .ToObject<List<ClientDataObject>>()
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


        private async Awaitable<string> CreateOrResume(string playerId, int seed)
        {
            string endpointURL = apiBaseUrl + "/sessions";
            string body;
            //For now seed won't ever be null
            if (playerId == null)
            {
                body = "{}";

            }
            else
            {
                body = JsonConvert.SerializeObject(new
                {
                    player_uuid = playerId,
                    _seed = seed
                });
            }
            

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
