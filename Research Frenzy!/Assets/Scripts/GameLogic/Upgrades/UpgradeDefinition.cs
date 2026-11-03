using UnityEngine;
namespace GameLogic.Upgrades
{
    /// <summary>
    /// UpgradeDefintion allows for modular upgrades to be added.
    /// This sets up one upgrade the player can buy and saves it as an asset
    ///
    /// To make an upgrade: Create > Research Frenzy > Upgrade, then fill in data points
    /// Then, add to Upgrades list in the controller
    ///
    /// </summary>
    
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "Research Frenzy/Upgrade")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Header("Shown on the shop card")]
        [Tooltip("Key used to save how many levels the player owns. Don't change it once there are saves.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName = "New Upgrade";
        [SerializeField, TextArea(2, 4)] private string description;
        [Tooltip("Picture of the item. Leave empty and the card shows the first letter of the name.")]
        [SerializeField] private Sprite icon;

        [Header("Levels and cost")]
        [SerializeField, Min(1)] private int maxLevel = 3;
        [Tooltip("Price of the first level.")]
        [SerializeField, Min(0)] private int baseCost = 150;
        [Tooltip("Added to the price for every level already owned. Cost = Base Cost + (Current Level x Cost Per Level).")]
        [SerializeField, Min(0)] private int costPerLevel = 50;

        [Header("What it does")]
        [SerializeField] private UpgradeEffect effect;
        [Tooltip("How much each level adds. Seconds for time effects, a count for clients and seats.")]
        [SerializeField] private float amountPerLevel = 1f;

        // falls back to the asset's file name if the Id box was left empty
        public string Id => string.IsNullOrWhiteSpace(id) ? name : id.Trim();
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxLevel => maxLevel;
        public UpgradeEffect Effect => effect;
        public float AmountPerLevel => amountPerLevel;

        /// <summary>Price to go from currentLevel to currentLevel + 1.</summary>
        public int CostForNextLevel(int currentLevel)
        {
            return baseCost + currentLevel * costPerLevel;
        }

        /// <summary>The total bonus at this many levels, e.g. Clock at level 3 = 15 seconds.</summary>
        public float AmountAt(int level)
        {
            return amountPerLevel * Mathf.Clamp(level, 0, maxLevel);
        }
    }
}
