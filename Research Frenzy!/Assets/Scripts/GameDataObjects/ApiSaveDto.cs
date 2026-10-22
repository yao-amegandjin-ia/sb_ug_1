using System.Collections.Generic;
using Newtonsoft.Json;
using Assets.Scripts.GameDataObjects;

namespace Assets.Scripts.Bootstrap.ApiDtos
{
    // These classes define the current json structure of the API. They define the JSON serializations so that they are seperate from 
    //the data objects the game will use, this is to seperate for the adapter pattern. 
    public class ApiSaveDto
    {
        [JsonProperty("player")]
        public ApiPlayerDto Player { get; set; }

        [JsonProperty("researchers")]
        public List<ApiResearcherDto> Researchers { get; set; }

        [JsonProperty("clients")]
        public List<ApiClientDto> Clients { get; set; }
    }

    public class ApiPlayerDto
    {
        [JsonProperty("current_money")]
        public int CurrentMoney { get; set; }

        [JsonProperty("total_money_earned")]
        public int TotalMoneyEarned { get; set; }

        [JsonProperty("current_day")]
        public int CurrentDay { get; set; }

        [JsonProperty("study_results")]
        public Dictionary<int, int> StudyResults { get; set; }

        [JsonProperty("owned_upgrades")]
        public Dictionary<string, int> OwnedUpgrades { get; set; }

        
    }

    public class ApiResearcherDto
    {
        [JsonProperty("researcher_id")]
        public int ResearcherId { get; set; }
        [JsonProperty("current_client_id")]
        public int CurrentClientId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("experience")]
        public int Experience { get; set; }

        [JsonProperty("speed")]
        public int Speed { get; set; }

        [JsonProperty("quality")]
        public int Quality { get; set; }

        [JsonProperty("methodologies")]
        public List<MethodologiesEnum> Methodologies { get; set; }

        [JsonProperty("skin")]
        public List<int> Skin { get; set; }
    }

    public class ApiClientDto
    {
        [JsonProperty("client_id")]
        public int ClientId { get; set; }

        [JsonProperty("request_id")]
        public int RequestId { get; set; }

        [JsonProperty("skin")]
        public List<int> Skin { get; set; }
    }

    public class ApiSessionDto
    {
        [JsonProperty("session_id")]
        public string SessionId { get; set; }

        [JsonProperty("player_uuid")]
        public string PlayerUuid { get; set; }

        [JsonProperty("last_played")]
        public System.DateTime? LastPlayed { get; set; }

        [JsonProperty("seed")]
        public int Seed { get; set; }
    }
}
