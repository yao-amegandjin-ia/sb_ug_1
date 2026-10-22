using Assets.Scripts.Bootstrap.ApiDtos;
using Assets.Scripts.GameDataObjects;
using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json.Linq;
using System.Text;

namespace Assets.Scripts.Bootstrap
{
    public static class SaveDataMapper
    {




        //Convert to the api dtos
        public static SaveData FromApiDto(ApiSaveDto api) => new SaveData
        {
            Player = api.Player == null ? null : new PlayerData
            {
                CurrentMoney = api.Player.CurrentMoney,
                TotalMoneyEarned = api.Player.TotalMoneyEarned,
                CurrentDay = api.Player.CurrentDay,
                StudyResults = api.Player.StudyResults ?? new Dictionary<int, int>(),
                OwnedUpgrades = api.Player.OwnedUpgrades ?? new Dictionary<string, int>()
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
                Skin = r.Skin ?? new List<int>(),
                ClientId = r.CurrentClientId
            }).ToList() ?? new List<ResearcherDataObject>(),
            Clients = api.Clients?.Select(c => new ClientDataObject
            {
                ClientId = c.ClientId,
                RequestId = c.RequestId,
                Skin = c.Skin ?? new List<int>()
            }).ToList() ?? new List<ClientDataObject>()
        };


        public static ApiSaveDto ToApiDto(SaveData save) => new ApiSaveDto
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
                CurrentClientId = r.ClientId,
                Skin = r.Skin

            }).ToList(),
            Clients = save.Clients?.Select(c => new ApiClientDto
            {
                ClientId = c.ClientId,
                RequestId = c.RequestId,
                Skin = c.Skin
            }).ToList()
        };
        public static ApiPlayerDto ToApiPlayer(PlayerData player) => new ApiPlayerDto
        {
            CurrentMoney = player.CurrentMoney,
            TotalMoneyEarned = player.TotalMoneyEarned,
            CurrentDay = player.CurrentDay,
            StudyResults = player.StudyResults,
            OwnedUpgrades = player.OwnedUpgrades
        };
    }
}
