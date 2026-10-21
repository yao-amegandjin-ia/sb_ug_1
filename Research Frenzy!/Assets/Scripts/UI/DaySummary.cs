// DaySummary - shows up when the day timer runs out.
// lists how the day went, then Continue starts the next day.

using UnityEngine;
using UnityEngine.UIElements;

public class DaySummary : MonoBehaviour
{
    VisualElement panel;
    VisualElement rows;
    Label title;
    Label earnedText;
    Label totalText;
    HUD hud;

    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        panel = root.Q("day-summary");
        rows = root.Q("summary-rows");
        title = root.Q<Label>("summary-title");
        earnedText = root.Q<Label>("summary-earned");
        totalText = root.Q<Label>("summary-total");
        root.Q<Button>("summary-continue").clicked += ContinueClicked;

        hud = GetComponent<HUD>();
        hud.DayEnded += Show;
    }

    void Show(int day)
    {
        title.text = "DAY " + day + " COMPLETE";

        rows.Clear();
        AddRow("Best answers", hud.bestToday);
        AddRow("Good answers", hud.partialToday);
        AddRow("Incorrect answers", hud.wrongToday);
        AddRow("Clients lost to patience", hud.lostToday);
        AddRow("Correct rejects", hud.rejectsToday);
        // TODO: for good / incorrect answers also show the best method and why

        earnedText.text = "$" + hud.earnedToday.ToString("N0");
        totalText.text = "$" + hud.money.ToString("N0");

        GetComponent<CodexPanel>().Close();
        GetComponent<ManageFirmPanel>().Close();
        panel.RemoveFromClassList("hidden");
    }

    void AddRow(string label, int value)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("summary-row");
        Label l = new Label(label);
        l.AddToClassList("summary-label");
        Label v = new Label(value.ToString());
        v.AddToClassList("summary-value");
        row.Add(l);
        row.Add(v);
        rows.Add(row);
    }

    void ContinueClicked()
    {
        panel.AddToClassList("hidden");

        if (hud.IsLastDay())
        {
            // TODO: final survey + leaderboard go here
            Debug.Log("Last day done - final survey would go here");
            return;
        }

        // TODO: open the upgrades screen here once it's done, then start the next day from there
        hud.NextDay();
    }
}
