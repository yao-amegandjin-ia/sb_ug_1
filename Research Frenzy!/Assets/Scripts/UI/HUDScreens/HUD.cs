// HUD - goes on the GameUI object. bottom bar: codex, manage firm, studies, money, day, timer.
// adding this script also adds the other UI scripts it needs (RequireComponent).
// other scripts should call AddMoney / AddStudyResult / SetOngoing instead of touching the labels.
// study results and correct rejects come from FirmController's events, so the screens don't have to tell the HUD.
// the day timer lives in FirmController. the HUD only shows it: Update reads FirmController.TimeLeft,
// and DayStarted resets the "today" counts.
// the labels are looked up in Bind (called by GameUIPanel when the PanelRenderer has built the UI),
// the game systems are found in Start. either one can run first.

using System;
using Bootstrap;
using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.HUDScreens
{
    [RequireComponent(typeof(GameUIPanel), typeof(StartScreen), typeof(DaySummary))]
    [RequireComponent(typeof(CodexPanel), typeof(ManageFirmPanel))]
    public class HUD : MonoBehaviour, IGameUIPart
    {
        // read straight from GameInitializer, so there is only one money value. DaySummary and ManageFirmPanel still use hud.money
        public int money => dataManager != null ? dataManager.PlayerData.CurrentMoney : 0;
        // read from FirmController, which owns the day. ManageFirmPanel still uses hud.day / hud.totalDays
        public int day => firmRules != null ? firmRules.CurrentDay : 1;
        public int totalDays => firmRules != null ? firmRules.TotalDays : 7;

        // whole game
        public int best, partial, wrong, ongoing;

        // just today, for the day summary
        public int bestToday, partialToday, wrongToday, lostToday, rejectsToday, earnedToday;

        private DataManager dataManager;

        int shownSeconds = -1;   // the whole seconds on the timer label, so it's only rewritten when it changes

        Label moneyText, dayText, timerText;
        Label bestText, partialText, wrongText, ongoingText;
        CodexPanel codex;
        //GameInitializer game;
        FirmController firmRules;   // game rules, in the Bootstrap scene. not the ManageFirmPanel below
        private ManageFirmPanel firm;

        void Awake()
        {
            codex = GetComponent<CodexPanel>();
            firm = GetComponent<ManageFirmPanel>();
        }

        // called by GameUIPanel once the UI has loaded (and again if it reloads)
        public void Bind(VisualElement root)
        {
            moneyText = root.Q<Label>("money-text");
            dayText = root.Q<Label>("day-text");
            timerText = root.Q<Label>("timer-text");
            bestText = root.Q<Label>("best-count");
            partialText = root.Q<Label>("partial-count");
            wrongText = root.Q<Label>("wrong-count");
            ongoingText = root.Q<Label>("ongoing-count");

            HookButton(root, "codex-button", ToggleCodex);
            HookButton(root, "manage-button", ToggleFirm);
            HookButton(root, "sound-button", () => Debug.Log("TODO: sound on/off"));
            HookButton(root, "settings-button", () => Debug.Log("TODO: settings"));
            HookButton(root, "help-button", () => Debug.Log("TODO: help"));

            Refresh();
        }

        void Start()
        {
        
            // the lab scene may not be loaded yet. if it isn't, UIInitializer.SendFirm calls Initialize(firm) later
            if (firmRules == null) Connect(FindAnyObjectByType<FirmController>());
            Refresh();
        }

        // Q<Button>(name).clicked += ... throws a NullReferenceException if the UXML has no button with that name.
        // this says which name is missing instead
        void HookButton(VisualElement root, string buttonName, Action onClick)
        {
            Button button = root.Q<Button>(buttonName);
            if (button != null) button.clicked += onClick;
            else Debug.LogWarning($"HUD: no Button named '{buttonName}' in the GameUI UXML.");
        }

        // FirmController counts the time down. this just shows it.
        // reading a property every frame is cheap, an event every frame would be noisier for no gain
        void Update()
        {
            ShowTime();
        }

        void ShowTime()
        {
            if (timerText == null) return;
            float timeLeft = firmRules != null ? firmRules.TimeLeft : 0f;
            int seconds = Mathf.CeilToInt(timeLeft);
            if (seconds == shownSeconds) return;   // same text as last frame
            shownSeconds = seconds;
            timerText.text = FormatTime(timeLeft);
        }
        public void Initialize(GameSystems systems)
        {
            dataManager = systems.DataManager;
        }
        public void Initialize(FirmController firm)
        {
            Connect(firm);
            GetComponent<DaySummary>().Initialize(firm);   // the day summary needs the same FirmController
        }

        // listens to the firm's events. safe to call twice: it lets go of the old firm first, so nothing is counted double
        void Connect(FirmController firm)
        {
            if (firm == firmRules && firm != null) return;
            Disconnect();
            firmRules = firm;
            if (firmRules == null) return;

            firmRules.MoneyEarned += OnMoneyEarned;
            firmRules.MoneySpent += OnMoneySpent;
            firmRules.StudyFinished += OnStudyFinished;
            firmRules.RequestRejected += OnRequestRejected;
            firmRules.ClientLost += OnClientLost;
            firmRules.DayStarted += OnDayStarted;
            firmRules.DayEnded += OnDayEnded;
            Refresh();
        }

        void Disconnect()
        {
            if (firmRules == null) return;
            firmRules.MoneyEarned -= OnMoneyEarned;
            firmRules.MoneySpent -= OnMoneySpent;
            firmRules.StudyFinished -= OnStudyFinished;
            firmRules.RequestRejected -= OnRequestRejected;
            firmRules.ClientLost -= OnClientLost;
            firmRules.DayStarted -= OnDayStarted;
            firmRules.DayEnded -= OnDayEnded;
            firmRules = null;
        }
        // a new day: clear today's counts for the day summary
        void OnDayStarted(int day)
        {
            bestToday = 0;
            partialToday = 0;
            wrongToday = 0;
            lostToday = 0;
            rejectsToday = 0;
            earnedToday = 0;
            Refresh();
        }

        // the timer label shows 0:00 straight away, even if this frame's Update already ran
        void OnDayEnded(int day)
        {
            Refresh();
        }

        void OnDestroy()
        {
            Disconnect();
        }

        void OnStudyFinished(StudyOutcome outcome)
        {
            AddStudyResult((int)outcome);
        }

        // the design doc counts turning away a request that could be researched as an incorrect answer
        void OnRequestRejected(bool correct)
        {
            if (correct) AddCorrectReject();
            else AddStudyResult(2);
        }

        // GameInitializer already added the money, this just counts it for today and updates the label
        void OnMoneyEarned(int amount)
        {
            if (amount > 0)
                earnedToday += amount;
            Refresh();
        }
 
        // a client left dissatisfied (no seat in the waiting area). shows in the day summary's "lost" row
        void OnClientLost(Client client)
        {
            AddLostClient();
        }

        // GameInitializer already took the money (an upgrade was bought), this just updates the label
        void OnMoneySpent(int amount)
        {
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
            if (moneyText == null) return;   // the UI hasn't loaded yet
            moneyText.text = "$" + money.ToString("N0");
            dayText.text = day + "/" + totalDays;
            shownSeconds = -1;   // force the timer label to be rewritten
            ShowTime();
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
}
