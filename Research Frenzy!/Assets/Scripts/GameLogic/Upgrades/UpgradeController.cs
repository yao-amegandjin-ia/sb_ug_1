// UpgradeController - the rules for buying upgrades and the bonuses they give.
// goes on the same GameObject as GameInitializer and FirmController in the Bootstrap scene.
// it only knows the upgrade assets in its list and how many levels of each the player owns.
// the scripts that use a bonus ask for it (HUD for day time, ClientGenerator for patience, ...),
// so this script never has to change when a new upgrade asset is added.

using Assets.Scripts.Bootstrap;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Upgrades
{
    /// <summary>One owned upgrade: asset id + level. A list of these shows in the Inspector and is what gets saved.</summary>
    [Serializable]
    public class UpgradeLevel
    {
        public string Id;
        public int Level;
    }
    
    /// <summary>
    /// UpgradeController has the rules for buying upgrades and their effects
    /// Same object as GameInitializer and FirmController
    /// Knows the upgrade assets in the list, and how many levels the player has for each
    /// The scripts that have the effect call here, so this doesn't change when new upgrade assets are added
    /// </summary>

    public class UpgradeController : MonoBehaviour
    {
        [Tooltip("Every upgrade in the shop, in the order the cards appear. Drag the Upgrade assets in here.")]
        [SerializeField] private List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        [Tooltip("Levels the player owns. Shown so you can watch it (or set it for testing) in Play mode.")]
        [SerializeField] private List<UpgradeLevel> owned = new List<UpgradeLevel>();

        /// <summary>An upgrade was bought. Gives the upgrade and its new level.</summary>
        public event Action<UpgradeDefinition, int> UpgradePurchased;

        /// <summary>Any level changed (bought, or loaded from a save). Listen to this to re-read a bonus.</summary>
        public event Action UpgradesChanged;

        private FirmController firmController;
        private DataManager dataManager;

        public IReadOnlyList<UpgradeDefinition> All => upgrades;

        private void Awake()
        {
            if (firmController == null) firmController = FindAnyObjectByType<FirmController>();
            if (firmController == null) Debug.LogError("UpgradeController: no GameInitializer found. Put this on the GameInitializer object.");

            CheckList();
        }
        public void Initialize(GameSystems systems, FirmController controller)
        {
            dataManager = systems.DataManager;
            firmController = controller;
        }

        // --- levels and prices ---

        public int GetLevel(UpgradeDefinition upgrade)
        {
            UpgradeLevel entry = Find(upgrade);
            return entry != null ? entry.Level : 0;
        }

        public bool IsMaxed(UpgradeDefinition upgrade)
        {
            return upgrade != null && GetLevel(upgrade) >= upgrade.MaxLevel;
        }

        /// <summary>Price of the next level, or 0 if it's maxed.</summary>
        public int GetNextCost(UpgradeDefinition upgrade)
        {
            if (upgrade == null || IsMaxed(upgrade)) return 0;
            return upgrade.CostForNextLevel(GetLevel(upgrade));
        }

        /// <summary>True if the next level can be bought right now (not maxed and the player has the money).</summary>
        public bool CanBuy(UpgradeDefinition upgrade)
        {
            if (upgrade == null || firmController == null || IsMaxed(upgrade)) return false;
            return dataManager.PlayerData.CurrentMoney >= GetNextCost(upgrade);
        }

        /// <summary>BUY. Takes the money and adds one level.</summary>
        /// <returns>False if it's maxed or the player can't afford it.</returns>
        public bool TryBuy(UpgradeDefinition upgrade)
        {
            if (!CanBuy(upgrade)) return false;

            int cost = GetNextCost(upgrade);
            if (!firmController.TrySpend(cost)) return false;

            int newLevel = GetLevel(upgrade) + 1;
            SetLevel(upgrade.Id, newLevel);
            Debug.Log($"Bought {upgrade.DisplayName} level {newLevel}/{upgrade.MaxLevel} for ${cost} (${dataManager.PlayerData.CurrentMoney} left)");

            UpgradePurchased?.Invoke(upgrade, newLevel);
            UpgradesChanged?.Invoke();
            return true;
        }

        // --- bonuses ---

        /// <summary>
        /// Everything owned that has this effect, added up. Works for any effect, including ones added later.
        /// e.g. Clock at level 2 with 5 per level: Total(DayLengthSeconds) = 10.
        /// </summary>
        public float Total(UpgradeEffect effect)
        {
            float total = 0f;
            foreach (UpgradeDefinition upgrade in upgrades)
            {
                if (upgrade != null && upgrade.Effect == effect)
                    total += upgrade.AmountAt(GetLevel(upgrade));
            }
            return total;
        }

        // shortcuts for the four effects that exist now, so callers don't have to round
        public float ExtraDaySeconds => Total(UpgradeEffect.DayLengthUpgrade);
        public int ExtraDailyClients => Mathf.RoundToInt(Total(UpgradeEffect.DailyClientsUpgrade));
        public int ExtraWaitingSeats => Mathf.RoundToInt(Total(UpgradeEffect.WaitingRoomSeatsUpgrade));
        public float ExtraPatienceSeconds => Total(UpgradeEffect.PatienceUpgrade);

        // --- saving ---
        
        //TODO: Not sure if these will work for the save data? 

        /// <summary>For the save file: upgrade id -> level. Matches PlayerData.OwnedUpgrades.</summary>
        public Dictionary<string, int> ToSaveData()
        {
            Dictionary<string, int> data = new Dictionary<string, int>();
            foreach (UpgradeLevel entry in owned)
                if (entry.Level > 0) data[entry.Id] = entry.Level;
            return data;
        }

        /// <summary>From a save file. Ids that no longer match an asset are skipped.</summary>
        public void LoadSaveData(Dictionary<string, int> data)
        {
            owned.Clear();
            if (data != null)
            {
                foreach (KeyValuePair<string, int> pair in data)
                {
                    UpgradeDefinition upgrade = FindDefinition(pair.Key);
                    if (upgrade == null)
                    {
                        Debug.LogWarning($"UpgradeController: save has upgrade '{pair.Key}', but no asset in the list has that id. Skipped.");
                        continue;
                    }
                    SetLevel(upgrade.Id, Mathf.Clamp(pair.Value, 0, upgrade.MaxLevel));
                }
            }
            UpgradesChanged?.Invoke();
        }

        // --- internals ---

        private UpgradeLevel Find(UpgradeDefinition upgrade)
        {
            if (upgrade == null) return null;
            return owned.Find(e => e.Id == upgrade.Id);
        }

        private UpgradeDefinition FindDefinition(string id)
        {
            return upgrades.Find(u => u != null && u.Id == id);
        }

        private void SetLevel(string id, int level)
        {
            UpgradeLevel entry = owned.Find(e => e.Id == id);
            if (entry == null) owned.Add(new UpgradeLevel { Id = id, Level = level });
            else entry.Level = level;
        }

        // two assets with the same id would share one level, so warn about it early
        private void CheckList()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (UpgradeDefinition upgrade in upgrades)
            {
                if (upgrade == null) { Debug.LogWarning("UpgradeController: there's an empty slot in the Upgrades list."); continue; }
                if (!ids.Add(upgrade.Id))
                    Debug.LogWarning($"UpgradeController: more than one upgrade has the id '{upgrade.Id}'. Give each asset its own id.");
            }
            if (upgrades.Count == 0)
                Debug.LogWarning("UpgradeController: the Upgrades list is empty, so the shop will have nothing to sell.");
        }
    }
}
