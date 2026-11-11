// ManageFirmPanel - left side panel with the firm's current state:
// hired researchers, upgrades, and stats for this session. it rebuilds every time it opens.
// upgrades come from the UpgradeController (buying them is in the shop screen, this only shows them).

using GameDataObjects;
using GameLogic.Upgrades;
using Researchers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.HUDScreens
{
    public class ManageFirmPanel : MonoBehaviour, IGameUIPart
    {
        VisualElement panel;
        VisualElement researcherList;
        VisualElement upgradeList;
        VisualElement statsGrid;
        HUD hud;
        StartScreen startScreen;
        private DataManager dataManager;

        void Awake()
        {
            hud = GetComponent<HUD>();
            startScreen = GetComponent<StartScreen>();
        }
        public void Initialize(DataManager dataManager)
        {
            this.dataManager = dataManager;
        }
        // called by GameUIPanel once the UI has loaded (and again if it reloads)
        public void Bind(VisualElement root)
        {
            panel = root.Q("manage-firm");
            researcherList = root.Q("firm-researchers");
            upgradeList = root.Q("firm-upgrades");
            statsGrid = root.Q("firm-stats");
            Button close = root.Q<Button>("firm-close");
            if (close != null) close.clicked += Close;
        }

        public void Open()
        {
            if (panel == null) return;   // the UI hasn't loaded yet
            RefreshResearchers();
            RefreshUpgrades();
            RefreshStats();
            panel.RemoveFromClassList("hidden");
        }

        public void Close()
        {
            if (panel != null) panel.AddToClassList("hidden");
        }

        public bool IsOpen()
        {
            return panel != null && !panel.ClassListContains("hidden");
        }

        void RefreshResearchers()
        {
            researcherList.Clear();
            int count = 0;

            
            if (dataManager != null)
            {
                foreach (ResearcherData r in dataManager.Researchers)
                {
                    if (!r.IsPurchased) continue;
                    string detail = "Cubicle " + r.Position + "  ·  Speed " + r.Speed + "  ·  Quality " + r.Quality + "  ·  XP " + r.Experience;
                    if (r.CurrentState == ResearcherState.Busy)
                        detail += "  ·  busy";
                    AddResearcherCard(r.Name, detail, r.KnownMethodologyIds);
                    count++;
                }
            }
            
            // nobody is hired through the generator yet, so show the starter from the start screen
            if (count == 0)
            {
                AddResearcherCard(startScreen.GetStarterName(), "Starting researcher", new int[] { startScreen.GetStarterMethod() });
            }
        }

        void AddResearcherCard(string name, string detail, System.Collections.Generic.IEnumerable<int> methods)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("firm-card");

            Label nameLabel = new Label(name);
            nameLabel.AddToClassList("firm-name");
            Label detailLabel = new Label(detail);
            detailLabel.AddToClassList("firm-detail");

            VisualElement badges = new VisualElement();
            badges.AddToClassList("badge-row");
            foreach (int id in methods)
            {
                Label badge = new Label(id.ToString());
                badge.AddToClassList("method-badge");
                badge.tooltip = MethodLibrary.GetName(id);
                badges.Add(badge);
            }

            card.Add(nameLabel);
            card.Add(detailLabel);
            card.Add(badges);
            researcherList.Add(card);
        }

        void RefreshUpgrades()
        {
            upgradeList.Clear();

            UpgradeController upgrades = FindAnyObjectByType<UpgradeController>();
            if (upgrades == null) return;

            foreach (UpgradeDefinition upgrade in upgrades.All)
            {
                if (upgrade == null) continue;
                int level = upgrades.GetLevel(upgrade);

                VisualElement card = new VisualElement();
                card.AddToClassList("firm-card");

                string levelText = level == 0 ? "Not bought yet" : "Level " + level + "/" + upgrade.MaxLevel;
                Label nameLabel = new Label(upgrade.DisplayName + "  (" + levelText + ")");
                nameLabel.AddToClassList("firm-name");
                Label desc = new Label(upgrade.Description);
                desc.AddToClassList("firm-detail");

                card.Add(nameLabel);
                card.Add(desc);
                upgradeList.Add(card);
            }
        }

        void RefreshStats()
        {
            statsGrid.Clear();
            AddStat("DAY", hud.day + "/" + hud.totalDays);
            AddStat("MONEY", "$" + hud.money.ToString("N0"));
            AddStat("ONGOING", hud.ongoing.ToString());
            AddStat("BEST", hud.best.ToString());
            AddStat("GOOD", hud.partial.ToString());
            AddStat("WRONG", hud.wrong.ToString());
        }

        void AddStat(string label, string value)
        {
            VisualElement stat = new VisualElement();
            stat.AddToClassList("stat");
            Label l = new Label(label);
            l.AddToClassList("stat-label");
            Label v = new Label(value);
            v.AddToClassList("stat-value");
            stat.Add(l);
            stat.Add(v);
            statsGrid.Add(stat);
        }
    }
}
