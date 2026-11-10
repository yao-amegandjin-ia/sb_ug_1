using System.Collections.Generic;

namespace GameDataObjects
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

        // methodology IDs from methodologies.csv (the old MethodologiesEnum is gone)
        public List<int> Methodologies
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

        // request ID -> the approach picked in step one of the match, as the ResearchApproach number
        // (0 None, 1 Quantitative, 2 Qualitative, 3 Mixed). Rejected and lost clients are saved as 0.
        public Dictionary<int, int> StudyApproaches { get; set; } = new();

        public Dictionary<string, int> OwnedUpgrades{ get; set; } = new();

        
    }
}