// HUD - goes on the GameUI object. bottom bar: codex, manage firm, studies, money, day, timer.
// adding this script also adds the other UI scripts it needs (RequireComponent).
// other scripts should call AddMoney / AddStudyResult / SetOngoing instead of touching the labels.

using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument), typeof(StartScreen), typeof(DaySummary))]
[RequireComponent(typeof(CodexPanel), typeof(ManageFirmPanel))]
public class HUD : MonoBehaviour
{
    // read straight from GameInitializer, so there is only one money value. DaySummary and ManageFirmPanel still use hud.money
    public int money => game != null ? game.currentMoney : 0;
    public int day = 1;
    public int totalDays = 7;
    public float dayLength = 180f; // seconds, placeholder until we decide

    // whole game
    public int best, partial, wrong, ongoing;

    // just today, for the day summary
    public int bestToday, partialToday, wrongToday, lostToday, rejectsToday, earnedToday;

    public event Action<int> DayEnded;

    float timeLeft;
    bool running = false;

    Label moneyText, dayText, timerText;
    Label bestText, partialText, wrongText, ongoingText;
    CodexPanel codex;
    GameInitializer game;
    ManageFirmPanel firm;

    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        moneyText = root.Q<Label>("money-text");
        dayText = root.Q<Label>("day-text");
        timerText = root.Q<Label>("timer-text");
        bestText = root.Q<Label>("best-count");
        partialText = root.Q<Label>("partial-count");
        wrongText = root.Q<Label>("wrong-count");
        ongoingText = root.Q<Label>("ongoing-count");
        
        game = FindAnyObjectByType<GameInitializer>();
        if (game != null) game.MoneyEarned += OnMoneyEarned;
        else Debug.LogWarning("HUD: no GameInitializer found, money will show $0. Did you press Play from the Bootstrap scene?");

        codex = GetComponent<CodexPanel>();
        firm = GetComponent<ManageFirmPanel>();

        root.Q<Button>("codex-button").clicked += ToggleCodex;
        root.Q<Button>("manage-button").clicked += ToggleFirm;
        root.Q<Button>("sound-button").clicked += () => Debug.Log("TODO: sound on/off");
        root.Q<Button>("settings-button").clicked += () => Debug.Log("TODO: settings");
        root.Q<Button>("help-button").clicked += () => Debug.Log("TODO: help");

        GetComponent<StartScreen>().GameStarted += (starter) => StartDay();

        timeLeft = dayLength;
        Refresh();
    }

    void Update()
    {
        if (!running) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
        {
            timeLeft = 0;
            running = false;
            Refresh();
            DayEnded?.Invoke(day);
        }
        else
        {
            timerText.text = FormatTime(timeLeft);
        }
    }

    public void StartDay()
    {
        bestToday = 0;
        partialToday = 0;
        wrongToday = 0;
        lostToday = 0;
        rejectsToday = 0;
        earnedToday = 0;
        timeLeft = dayLength;
        running = true;
        Refresh();
    }

    public void NextDay()
    {
        day++;
        StartDay();
    }

    public bool IsLastDay()
    {
        return day >= totalDays;
    }

    void OnDestroy()
    {
        if (game != null) game.MoneyEarned -= OnMoneyEarned;
    }

    // GameInitializer already added the money, this just counts it for today and updates the label
    void OnMoneyEarned(int amount)
    {
        if (amount > 0)
            earnedToday += amount;
        Refresh();
    }
 
    // 0 = best, 1 = partial (good), 2 = wrong
    public void AddStudyResult(int result)
    {
        if (result == 0) { best++; bestToday++; }
        else if (result == 1) { partial++; partialToday++; }
        else { wrong++; wrongToday++; }
        Refresh();
    }
    public void AddLostClient()
    {
        lostToday++;
    }

    public void AddCorrectReject()
    {
        rejectsToday++;
    }

    public void SetOngoing(int count)
    {
        ongoing = count;
        Refresh();
    }

    void ToggleCodex()
    {
        if (codex.IsOpen())
        {
            codex.Close();
        }
        else
        {
            firm.Close(); // both use the left side, only one at a time
            codex.Open();
        }
    }

    void ToggleFirm()
    {
        if (firm.IsOpen())
        {
            firm.Close();
        }
        else
        {
            codex.Close();
            firm.Open();
        }
    }

    void Refresh()
    {
        if (moneyText == null) return;
        moneyText.text = "$" + money.ToString("N0");
        dayText.text = day + "/" + totalDays;
        timerText.text = FormatTime(timeLeft);
        bestText.text = best.ToString();
        partialText.text = partial.ToString();
        wrongText.text = wrong.ToString();
        ongoingText.text = ongoing.ToString();
    }

    string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
