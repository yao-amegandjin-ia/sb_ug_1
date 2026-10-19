// ManageFirmPanel - left side panel with the firm's current state:
// hired researchers, upgrades, and stats for this session. it rebuilds every time it opens.

using Researchers;
using UnityEngine;
using UnityEngine.UIElements;

public class ManageFirmPanel : MonoBehaviour
{
    // placeholder until the upgrades system exists
    public string[] upgradeNames = { "Faster Stations", "Bigger Waiting Area", "Campus Flyers", "Department Outreach" };
    public string[] upgradeDescriptions =
    {
        "Consultations finish sooner.",
        "Clients wait longer before leaving.",
        "More clients come in each day.",
        "Higher paying clients come in."
    };
    public int[] upgradeLevels = { 0, 0, 0, 0 };

    VisualElement panel;
    VisualElement researcherList;
    VisualElement upgradeList;
    VisualElement statsGrid;
    HUD hud;
    StartScreen startScreen;

    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        panel = root.Q("manage-firm");
        researcherList = root.Q("firm-researchers");
        upgradeList = root.Q("firm-upgrades");
        statsGrid = root.Q("firm-stats");
        root.Q<Button>("firm-close").clicked += Close;

        hud = GetComponent<HUD>();
        startScreen = GetComponent<StartScreen>();
    }

    public void Open()
    {
        RefreshResearchers();
        RefreshUpgrades();
        RefreshStats();
        panel.RemoveFromClassList("hidden");
    }

    public void Close()
    {
        panel.AddToClassList("hidden");
    }

    public bool IsOpen()
    {
        return !panel.ClassListContains("hidden");
    }

    void RefreshResearchers()
    {
        researcherList.Clear();
        int count = 0;

        ResearcherGenerator generator = FindAnyObjectByType<ResearcherGenerator>();
        if (generator != null)
        {
            foreach (ResearcherData r in generator.Researchers)
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
        for (int i = 0; i < upgradeNames.Length; i++)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("firm-card");

            string level = upgradeLevels[i] == 0 ? "Not bought yet" : "Level " + upgradeLevels[i];
            Label nameLabel = new Label(upgradeNames[i] + "  (" + level + ")");
            nameLabel.AddToClassList("firm-name");
            Label desc = new Label(upgradeDescriptions[i]);
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
        AddStat("PARTIAL", hud.partial.ToString());
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
