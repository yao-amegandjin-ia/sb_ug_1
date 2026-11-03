// ShopTab - base class for one tab of the shop screen (Upgrades, Hire, ...).
// put tab components on the same GameObject as the ShopScreen. the shop makes one tab button per component,
// in the order they're listed in the Inspector, and opens on the first one.
// to add a new tab later: make a class that extends ShopTab, write Fill(), and add it to the ShopUI object.

using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;
using GameLogic.Upgrades;

namespace UI
{
    /// <summary>
    /// Base abstract class for a tab on the shop screen
    /// Tab components are on the same object as ShopScreen. One tab per coponent
    /// To add new tabs, make a class that extends ShopTab and add to ShopUI
    /// </summary>
    [RequireComponent(typeof(ShopScreen))]
    public abstract class ShopTab : MonoBehaviour
    {
        [Tooltip("Text on this tab's button.")]
        [SerializeField] private string tabTitle = "TAB";

        private ShopScreen shop;
        private FirmController firm;
        private UpgradeController upgrades;

        public string Title => tabTitle;

        /// <summary>
        /// Add this tab's cards to the list. The list is already empty. Runs when the tab is picked
        /// and again after every Refresh(), so just build from the current game state each time.
        /// </summary>
        public abstract void Fill(VisualElement list);

        /// <summary>Call after something was bought or hired. Rebuilds the tab and keeps the scroll position.</summary>
        protected void Refresh()
        {
            if (shop == null) shop = GetComponent<ShopScreen>();
            shop.Refresh();
        }

        // the game rules are in the Bootstrap scene, so they're found at runtime (once)
        protected FirmController Firm
        {
            get
            {
                if (firm == null) firm = FindAnyObjectByType<FirmController>();
                if (firm == null) Debug.LogWarning($"{GetType().Name}: no FirmController found. Is it on the GameInitializer object?");
                return firm;
            }
        }

        protected UpgradeController Upgrades
        {
            get
            {
                if (upgrades == null) upgrades = FindAnyObjectByType<UpgradeController>();
                if (upgrades == null) Debug.LogWarning($"{GetType().Name}: no UpgradeController found. Is it on the GameInitializer object?");
                return upgrades;
            }
        }

        // --- helpers for filling in cards ---

        protected static void SetText(VisualElement card, string elementName, string text)
        {
            TextElement element = card.Q<TextElement>(elementName);
            if (element != null) element.text = text;
        }

        // the grey message shown when a tab has nothing in it
        protected static void AddEmptyMessage(VisualElement list, string text)
        {
            Label label = new Label(text);
            label.AddToClassList("shop-empty");
            list.Add(label);
        }
    }
}
