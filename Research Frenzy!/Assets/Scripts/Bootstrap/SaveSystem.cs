using Assets;
using Assets.Scripts.Bootstrap.ApiDtos;
using Assets.Scripts.GameDataObjects;
using Assets.Scripts.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
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
        /// Constructor for the savesystem class.
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
            _lastPlayed = response["last_played"]?.Value<DateTime>()
              ?? DateTime.UtcNow;
            _seed = response["seed"].Value<int>();
            //We set the seed in the static class as well so that it is not mismatched!
            GameSeed.Set(_seed);

            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/save";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(endpointURL))
            {
                await webRequest.SendWebRequest();

                //Empty save data if no session exists yet.
                if (webRequest.responseCode == 404)
                {
                    return new SaveData();
                }
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception(
                        $"{webRequest.responseCode}: {webRequest.error}"
                    );
                }

                //Convert the response to the api object
                ApiSaveDto apiSave = JsonConvert.DeserializeObject<ApiSaveDto>(webRequest.downloadHandler.text);
                if (apiSave == null)
                {
                    throw new Exception("The API returned an empty save response.");

                }
                return FromApiDto(apiSave);
                
            }
        }

        public override async Awaitable SaveGame(SaveData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new InvalidOperationException("Call LoadGame() to create or resume a session before saving.");
            }

            await CheckServerHealth();
            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/save";

            ApiSaveDto apiSave = ToApiDto(saveData);
            string body = JsonConvert.SerializeObject(apiSave);
            
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
            //For now seed should not ever be null
            if (playerId == null)
            {
                body = "{}";

            }
            else
            {
                body = JsonConvert.SerializeObject(new
                {
                    player_uuid = playerId,
                    seed = seed
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
       //Provide a null PlayerData if not sending back a finalPlayerState
        public async override Awaitable EndGame(PlayerData finalPlayerState)
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new InvalidOperationException("Call LoadGame() to create or resume a session before saving.");
            }
            await CheckServerHealth();
            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/end";
            string body;
            if (finalPlayerState == null)
            {
                body = "{}";
            }
            else
            {
                body = JsonConvert.SerializeObject(ToApiPlayer(finalPlayerState));
            }
            

            using (UnityWebRequest webRequest = UnityWebRequest.Post(endpointURL, body, "application/json"))
            {
                await webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"{webRequest.responseCode}: {webRequest.error}");
                }
            }
        }

        public async override Awaitable SaveQuizData(QuizType quizType, Dictionary<int, int> responses)
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new InvalidOperationException("Call LoadGame() to create or resume a session before saving.");
            }
            await CheckServerHealth();
            string quiz;
            if (quizType == QuizType.MidGame)
            {
                quiz = "mid-game";
            }
            else
            {
                quiz = "post-game";
            }

            string endpointURL = apiBaseUrl + $"/sessions/{_sessionId}/quizzes/{quiz}";

            string body = JsonConvert.SerializeObject(
                new
                {
                    responses = responses
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

        //Convert to the api dtos
        private static SaveData FromApiDto(ApiSaveDto api) => new SaveData
        {
            Player = api.Player == null ? null : new PlayerData
            {
                CurrentMoney = api.Player.CurrentMoney,
                TotalMoneyEarned = api.Player.TotalMoneyEarned,
                CurrentDay = api.Player.CurrentDay,
                StudyResults = api.Player.StudyResults ?? new Dictionary<int, int>(),
                OwnedUpgrades = api.Player.OwnedUpgrades ?? new Dictionary<string, int>(),
                HiredResearchers = api.Player.HiredResearchers ?? new List<string>()
            },
            Researchers = api.Researchers?.Select(r => new ResearcherDataObject
            {
                ResearcherId = r.ResearcherId,
                Name = r.Name,
                Postion = r.Position,
                Experience = r.Experience,
                Speed = r.Speed,
                Quality = r.Quality,
                Methodologies = r.Methodologies ?? new List<MethodologiesEnum>(),
                Skin = r.Skin ?? new List<int>()
            }).ToList() ?? new List<ResearcherDataObject>(),
            Clients = api.Clients?.Select(c => new ClientDataObject
            {
                ClientId = c.ClientId,
                RequestId = c.RequestId,
                Skin = c.Skin ?? new List<int>()
            }).ToList() ?? new List<ClientDataObject>()
        };
    }

     private static ApiSaveDto ToApiDto(SaveData save) => new ApiSaveDto
     {
         Player = save.Player == null ? null : ToApiPlayer(save.Player),
         Researchers = save.Researchers?.Select(r => new ApiResearcherDto
         {
             ResearcherId = r.ResearcherId,
             Name = r.Name,
             Position = r.Postion, // Rename to r.Position if the game model typo is fixed.
             Experience = r.Experience,
             Speed = r.Speed,
             Quality = r.Quality,
             Methodologies = r.Methodologies,
             Skin = r.Skin
         }).ToList(),
         Clients = save.Clients?.Select(c => new ApiClientDto
         {
             ClientId = c.ClientId,
             RequestId = c.RequestId,
             Skin = c.Skin
         }).ToList()
     };
        private static ApiPlayerDto ToApiPlayer(PlayerData player) => new ApiPlayerDto
        {
            CurrentMoney = player.CurrentMoney,
            TotalMoneyEarned = player.TotalMoneyEarned,
            CurrentDay = player.CurrentDay,
            StudyResults = player.StudyResults,
            OwnedUpgrades = player.OwnedUpgrades,
            HiredResearchers = player.HiredResearchers
        };



    }
}
