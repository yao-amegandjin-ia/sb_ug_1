using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameDataObjects
{
    public class SaveData
    {
        
        public PlayerData Player { get; set; } = new();

        public List<ResearcherData> Researchers { get; set; } = new();

        public List<ClientData> Clients { get; set; } = new();

    }
}
