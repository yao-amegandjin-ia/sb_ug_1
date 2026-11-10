using System.Collections.Generic;
using System.Linq;
using Clients;
using GameDataObjects;
using Researchers;
using UnityEngine;

namespace Bootstrap
{
    public static class SaveDataMapper
    {

        //Convert to the api dtos
        public static SaveData FromApiDto(ApiSaveDto api, int seed) => new SaveData
        {
            Seed = seed,
            Player = api.Player == null ? null : new PlayerData
            {
                CurrentMoney = api.Player.CurrentMoney,
                TotalMoneyEarned = api.Player.TotalMoneyEarned,
                CurrentDay = api.Player.CurrentDay,
                StudyResults = api.Player.StudyResults ?? new Dictionary<int, int>(),
                // saves made before approaches were recorded don't have this, so start empty
                StudyApproaches = api.Player.StudyApproaches ?? new Dictionary<int, int>(),
                OwnedUpgrades = api.Player.OwnedUpgrades ?? new Dictionary<string, int>()
            },


            Researchers = api.Researchers?.Select(r =>
            {
                var researcher = new ResearcherData();
                var skin = r.Skin ?? new List<int>();

                var appearance = skin.Count >= 3
                    ? new Vector3Int(skin[0], skin[1], skin[2])
                    : Vector3Int.zero;

                researcher.Init(
                    
                    r.ResearcherId,
                    r.Name,
                    appearance,
                    r.Experience,
                    r.Speed,
                    r.Quality,
                    r.Methodologies,
                    r.Position
                );


                return researcher;
            }).ToList() ?? new List<ResearcherData>(),

            Clients = api.Clients?.Select(c =>
            {
                var client = new ClientData();
                var skin = c.Skin ?? new List<int>();

                var appearance = skin.Count >= 3
                    ? new Vector3Int(skin[0], skin[1], skin[2])
                    : Vector3Int.zero;

                //Use default patience
                client.Init(
                    c.ClientId, c.RequestId, appearance
                    );
                return client;


            }).ToList() ?? new List<ClientData>()
        };





        public static ApiSaveDto ToApiDto(SaveData save) => new ApiSaveDto
        {
            Player = save.Player == null ? null : ToApiPlayer(save.Player),
            Researchers = save.Researchers?.Select(r => new ApiResearcherDto
            {
               
                ResearcherId = r.Id,
                Name = r.Name,
                Position = r.Position, 
                Experience = r.Experience,
                Speed = r.Speed,
                Quality = r.Quality,
                Methodologies = r.KnownMethodologyIds?.ToList() ?? new List<int>(),
                Skin = new List<int>
                {
                    r.Appearance.x,
                    r.Appearance.y,
                    r.Appearance.z
                }

            }).ToList(),

            Clients = save.Clients?.Select(c => new ApiClientDto
            {
                ClientId = c.Id,
                RequestId = c.RequestId,
                Skin = new List<int>
                {
                    c.Appearance.x,
                    c.Appearance.y,
                    c.Appearance.z
                }
            }).ToList()
        };
        public static ApiPlayerDto ToApiPlayer(PlayerData player) => new ApiPlayerDto
        {
            CurrentMoney = player.CurrentMoney,
            TotalMoneyEarned = player.TotalMoneyEarned,
            CurrentDay = player.CurrentDay,
            StudyResults = player.StudyResults,
            StudyApproaches = player.StudyApproaches,
            OwnedUpgrades = player.OwnedUpgrades
        };
    }
}
