using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameObjects
{
    public class ResearcherSaveData
    {
        [JsonProperty("researcher_uuid")]

        public string ResearcherId { get; set; }
        public string Name { get; set; }

        public int Experience { get; set; }
        public int Speed { get; set; }
        public int Quality { get; set; }

        public List<MethodologiesEnum> Methodologies
        { get; set; } = new();

        public List<int> Skin { get; set; } = new();

    }
    public class ClientSaveData
    {
        [JsonProperty("client_uuid")]

        public string ClientId { get; set; }
        [JsonProperty("request_id")]

        public int RequestId { get; set; }

        public string Name { get; set; }
        public List<int> Skin { get; set; } = new();
    }
}
