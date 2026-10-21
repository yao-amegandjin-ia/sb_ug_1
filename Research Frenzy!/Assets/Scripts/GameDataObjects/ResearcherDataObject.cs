using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameDataObjects
{
    public class ResearcherDataObject
    {
        [JsonProperty("researcher_id")]
        public int ResearcherId { get; set; }
        public string Name { get; set; }
       
        public int Postion { get; set; }
        public int Experience { get; set; }
        public int Speed { get; set; }
        public int Quality { get; set; }

        public List<MethodologiesEnum> Methodologies
        { get; set; } = new();

        public List<int> Skin { get; set; } = new();

    }
    public class ClientDataObject
    {
        [JsonProperty("client_id")]

        public int ClientId { get; set; }
        [JsonProperty("request_id")]

        public int RequestId { get; set; }

        public string Name { get; set; }
        public List<int> Skin { get; set; } = new();
    }
    public class PlayerData
    {
        
        public int CurrentMoney { get; set; }

        public int TotalMoneyEarned { get; set; }
        public int CurrentDay { get; set; }

        public Dictionary<int, int> StudyResults { get; set; } = new();

        public Dictionary<string, int> OwnedUpgrades{ get; set; } = new();

        //ResearcherIds
        public List<string> HiredResearchers { get; set; } = new(); 
    }
}
