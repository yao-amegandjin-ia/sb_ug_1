using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameObjects
{
    public class SaveData
    {
        public string PlayerId { get; set; }

        public string SessionId { get; set; }
        
        public int CurrentMoney { get; set; }

        public int TotalMoneyEarned { get; set; }
        public int CurrentDay { get; set; }

        public Dictionary<int, MethodologiesEnum> StudyResults{ get; set; } = new();

        public Dictionary<string, int> OwnedUpgrades
        { get; set; } = new();

        public List<ResearcherSaveData> Researchers
        { get; set; } = new();

        public List<ClientSaveData> Clients
        { get; set; } = new();

    }
}
