// ShopScreen - goes on the ShopUI object in the UI scene (PanelRenderer with ShopScreen.uxml).
// one window with a tab button for every ShopTab component on this same GameObject (Upgrades, Hire, ...).
// it always opens on the first tab. buying or hiring doesn't close it, the tab just rebuilds.
// the player leaves with the CLOSE button. for now the HUD's Upgrades button opens it.
// Opens when a day ends (OpenAtDayEnd), before the day summary. Then the close button
// says "DAY SUMMARY" and closing the shop fires ClosedAtDayEnd, which UIInitializer turns into the day summary.

using GameLogic;
using System;
using System.Collections.Generic;
using Bootstrap;
using UI.ClientScreens;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// ShopScreen goes on the ShopUI object in the UI scene
    /// One screen with a tab button for all shop tabs
    /// Opens on the first tab, and reloads when purchases are done
    /// Player leaves with close button
    /// </summary>
    public class ShopScreen : ModalScreen
    {
        private readonly List<Button> tabButtons = new List<Button>();
        private ShopTab[] tabs;
        private int currentTab;

        private VisualElement tabBar;
        private ScrollView list;
        private Label moneyText;

        private DataManager dataManager;
        private FirmController firmController;
        
        /// <summary>
        /// The shop that opened at the end of a day was closed (DAY SUMMARY / close). UIInitializer opens the day summary on this.
        /// Not fired when the shop is opened and closed in the middle of a day.
        /// </summary>
        public event Action ClosedAtDayEnd;

        private bool atDayEnd;             // true while the shop is the end-of-day screen
        private int endedDay;              // the day that just ended: "END OF DAY 2"
        private Button closeButton;
        private string closeButtonText;    // whatever the UXML says ("CLOSE"), put back after the end-of-day shop
        private Label titleText;           // optional "shop-title" label in the UXML
        private string titleTextDefault;

        protected override void Awake()
        {
            // before base.Awake, because the PanelRenderer can call BindUI straight away and that needs the tabs
            tabs = GetComponents<ShopTab>();
            if (tabs.Length == 0)
                Debug.LogWarning("ShopScreen: no tabs. Add UpgradesTab and HireTab to this GameObject.");
            base.Awake();
        }
        public void Initialize(GameSystems systems)
        {
            dataManager = systems.DataManager;
        }
        public void Initialize(FirmController firm)
        {
            firmController = firm;
        }

        private void Start()
        {
            // money changes while the shop is open (studies finishing in the lab), so the buttons and label follow it
            
            if (firmController != null)
            {
                firmController.MoneyEarned += OnMoneyChanged;
                firmController.MoneySpent += OnMoneyChanged;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (firmController != null)
            {
                firmController.MoneyEarned -= OnMoneyChanged;
                firmController.MoneySpent -= OnMoneyChanged;
            }
        }

        protected override void BindUI(VisualElement root)
        {
            tabBar = Find<VisualElement>("tab-bar");
            list = Find<ScrollView>("shop-list");
            moneyText = Find<Label>("shop-money");
            HookButton("close-button", Close);
            BuildTabButtons();
            
            closeButton = Root.Q<Button>("close-button");
            closeButtonText = closeButton != null ? closeButton.text : "CLOSE";
            titleText = Root.Q<Label>("shop-title");
            titleTextDefault = titleText != null ? titleText.text : "";
            ApplyDayEndText();   // the UI can reload while the end-of-day shop is open
        }

        // one button per tab component. the template's own buttons (if any) are cleared first
        private void BuildTabButtons()
        {
            tabButtons.Clear();
            if (tabBar == null) return;
            tabBar.Clear();

            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;   // copy for the lambda
                Button button = new Button(() => SelectTab(index));
                button.text = tabs[i].Title;
                button.AddToClassList("shop-tab");
                tabBar.Add(button);
                tabButtons.Add(button);
            }
        }

        // --- opening ---

        /// <summary>Opens on the first tab (Upgrades).</summary>
        public void Open() => Open(0);
        
        /// <summary>
        /// Opens the shop as the first screen after a day ends (before the day summary). It opens on the Upgrades tab,
        /// the close button says "DAY SUMMARY", and closing it fires ClosedAtDayEnd.
        /// </summary>
        public void OpenAtDayEnd(int day)
        {
            atDayEnd = true;
            endedDay = day;
            ApplyDayEndText();
            Open<UpgradesTab>();
        }
        protected override void OnHidden()
        {
            base.OnHidden();
            if (!atDayEnd) return;

            atDayEnd = false;       // first, so a second Close() can't open the summary twice
            ApplyDayEndText();
            ClosedAtDayEnd?.Invoke();
        }
        
        private void ApplyDayEndText()
        {
            if (closeButton != null)
                closeButton.text = atDayEnd ? "DAY SUMMARY" : closeButtonText;
            if (titleText != null)
                titleText.text = atDayEnd ? "END OF DAY " + endedDay : titleTextDefault;
        }

        /// <summary>Opens on a tab, counted from 0 in the order the tab components are listed.</summary>
        public void Open(int tabIndex)
        {
            currentTab = Mathf.Clamp(tabIndex, 0, Mathf.Max(0, tabs.Length - 1));
            Show();
        }

        /// <summary>Opens on the first tab of this type</summary>
        public void Open<T>() where T : ShopTab
        {
            int index = System.Array.FindIndex(tabs, t => t is T);
            Open(index < 0 ? 0 : index);
        }

        // the HUD's Upgrades button calls this
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        protected override void OnShown()
        {
            SelectTab(currentTab);
        }

        // --- tabs ---

        private void SelectTab(int index)
        {
            if (tabs.Length == 0) return;
            currentTab = Mathf.Clamp(index, 0, tabs.Length - 1);

            for (int i = 0; i < tabButtons.Count; i++)
                tabButtons[i].EnableInClassList("selected", i == currentTab);

            Rebuild(keepScroll: false);
        }

        /// <summary>Rebuilds the open tab with the latest money and levels. Tabs call this after a purchase.</summary>
        public void Refresh()
        {
            if (IsOpen && IsReady) Rebuild(keepScroll: true);
        }

        private void Rebuild(bool keepScroll)
        {
            if (moneyText != null) moneyText.text = "$" + (dataManager != null ? dataManager.PlayerData.CurrentMoney : 0).ToString("N0");
            if (list == null || tabs.Length == 0) return;

            Vector2 scroll = list.scrollOffset;
            list.Clear();
            tabs[currentTab].Fill(list);

            // the new cards haven't been laid out yet, so put the scroll back on the next frame
            if (keepScroll) list.schedule.Execute(() => list.scrollOffset = scroll);
            else list.scrollOffset = Vector2.zero;
        }

        private void OnMoneyChanged(int amount)
        {
            Refresh();
        }
    }
}
