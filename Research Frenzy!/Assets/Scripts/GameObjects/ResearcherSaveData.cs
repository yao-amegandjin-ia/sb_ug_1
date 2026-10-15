using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.GameObjects
{
    public class ResearcherSaveData
    {

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
        public string ClientId { get; set; }
        public int RequestId { get; set; }

        public string Name { get; set; }
        public List<int> Skin { get; set; } = new();
    }
}
