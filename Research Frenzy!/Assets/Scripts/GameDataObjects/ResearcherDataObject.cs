using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameDataObjects
{
    public class ResearcherDataObject
    {
        public int ResearcherId { get; set; }

        public int ClientId { get; set; }
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
    
        public int ClientId { get; set; }
       
        public int RequestId { get; set; }
        public List<int> Skin { get; set; } = new();
    }
    public class PlayerData
    {
        
        public int CurrentMoney { get; set; }

        public int TotalMoneyEarned { get; set; }
        public int CurrentDay { get; set; }

        public Dictionary<int, int> StudyResults { get; set; } = new();

        public Dictionary<string, int> OwnedUpgrades{ get; set; } = new();

        
    }
}
