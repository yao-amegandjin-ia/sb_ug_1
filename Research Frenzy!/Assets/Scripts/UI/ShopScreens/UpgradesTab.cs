// UpgradesTab - the shop's upgrades tab. goes on the ShopUI object next to the ShopScreen.
// one UpgradeCard.uxml per upgrade in the UpgradeController's list: picture, name, description, levels, BUY.
// buying is UpgradeController.TryBuy. this only shows the cards and passes the click on.

using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;
using GameLogic.Upgrades;

namespace UI
{
    public class UpgradesTab : ShopTab
    {
        // drag UpgradeCard.uxml in here (UI/Shop Screen)
        [SerializeField] private VisualTreeAsset upgradeCard;

        public override void Fill(VisualElement list)
        {
            UpgradeController upgrades = Upgrades;
            if (upgrades == null) return;

            if (upgradeCard == null)
            {
                Debug.LogWarning("UpgradesTab: no Upgrade Card set. Drag UpgradeCard.uxml into it.");
                return;
            }

            int shown = 0;
            foreach (UpgradeDefinition upgrade in upgrades.All)
            {
                if (upgrade == null) continue;
                list.Add(BuildCard(upgrade, upgrades));
                shown++;
            }

            if (shown == 0) AddEmptyMessage(list, "No upgrades for sale yet.");
        }

        // the layout is all in UpgradeCard.uxml / .uss. this only fills in the named elements
        private VisualElement BuildCard(UpgradeDefinition upgrade, UpgradeController upgrades)
        {
            VisualElement card = upgradeCard.Instantiate();
            int level = upgrades.GetLevel(upgrade);

            SetText(card, "upgrade-name", upgrade.DisplayName);
            SetText(card, "upgrade-description", upgrade.Description);
            SetText(card, "level-text", $"LEVEL {level}/{upgrade.MaxLevel}");

            // picture of the item. until there's a sprite, the box shows the first letter of the name
            VisualElement icon = card.Q("upgrade-icon");
            Label placeholder = card.Q<Label>("icon-placeholder");
            if (upgrade.Icon != null)
            {
                if (icon != null) icon.style.backgroundImage = new StyleBackground(upgrade.Icon);
                if (placeholder != null) placeholder.style.display = DisplayStyle.None;
            }
            else if (placeholder != null)
            {
                placeholder.text = upgrade.DisplayName.Length > 0 ? upgrade.DisplayName.Substring(0, 1).ToUpper() : "?";
            }

            // one pip per level, filled for the levels owned
            VisualElement pips = card.Q("level-pips");
            if (pips != null)
            {
                pips.Clear();
                for (int i = 0; i < upgrade.MaxLevel; i++)
                {
                    VisualElement pip = new VisualElement();
                    pip.AddToClassList("level-pip");
                    pip.EnableInClassList("filled", i < level);
                    pips.Add(pip);
                }
            }

            Button buy = card.Q<Button>("buy-upgrade");
            if (buy != null)
            {
                if (upgrades.IsMaxed(upgrade))
                {
                    buy.text = "MAXED";
                    buy.AddToClassList("maxed");
                    buy.SetEnabled(false);
                }
                else
                {
                    buy.text = "$" + upgrades.GetNextCost(upgrade).ToString("N0");
                    buy.SetEnabled(upgrades.CanBuy(upgrade));   // greyed out when the player can't afford it
                    buy.clicked += () => BuyClicked(upgrade);
                }
            }

            return card;
        }

        // the screen stays open and the cards rebuild with the new level, price and money
        private void BuyClicked(UpgradeDefinition upgrade)
        {
            if (Upgrades != null && Upgrades.TryBuy(upgrade))
                Refresh();
        }
    }
}
