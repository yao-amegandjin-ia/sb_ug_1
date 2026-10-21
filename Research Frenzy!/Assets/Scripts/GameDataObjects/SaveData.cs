using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameDataObjects
{
    public class SaveData
    {
        
        public PlayerData Player { get; set; } = new();

        public List<ResearcherDataObject> Researchers { get; set; } = new();

        public List<ClientDataObject> Clients { get; set; } = new();

    }
}
