// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).
//   day ends -> shop (upgrades + hire) -> DAY SUMMARY button -> day summary -> Continue -> (day 4: mid-game quiz) -> next day.
// The shop's close button opens the summary (ShopScreen.ClosedAtDayEnd -> OnShopClosedAtDayEnd).
// After the last day there's no shop: the summary opens straight away, then the final quiz.
//the summary's SAVE & CONTINUE / SAVE & QUIT save the game first. A save is made AT the day summary,
// so CONTINUE on the start screen carries on from that point: AfterDaySummary below, the same as pressing
// SAVE & CONTINUE. SAVE & QUIT reloads the game, which brings back the start screen.

using Bootstrap;
using GameLogic;
using Interfaces;
using System.Collections.Generic;
using UI;
using UI.ClientScreens;
using UI.HUDScreens;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;
using DataLogging;

public class UIInitializer : MonoBehaviour, ISceneInitializable
{


    [SerializeField] private ModalScreen[] screensToClose;
    private DaySummary daySummary;
    private CodexPanel codex;
    private ManageFirmPanel manageFirm;

    private QuizScreen quiz;
    public GameObject UI;
    public GameObject shopUI;

    private FirmController connectedFirm;
    private HUD hud;

    private ShopScreen shop;
    private StartScreen startScreen;
    private GameSystems systems;
    private int summaryDay = -1;   //: the day whose summary opens when the shop closes, -1 = none

    // the persistent systems, once the bootstrap has handed them over. null before that
    public GameSystems Systems => systems;

    public void InitializeScene(GameSystems gameSystems)
    {
        systems = gameSystems;
        Debug.Log("UI scene initialized");
        daySummary = UI.GetComponent<DaySummary>();
        codex = UI.GetComponent<CodexPanel>();
        quiz = UI.GetComponent<QuizScreen>();
        hud = UI.GetComponent<HUD>();
        shop = shopUI.GetComponent<ShopScreen>();
        startScreen = UI.GetComponent<StartScreen>();
        manageFirm = UI.GetComponent<ManageFirmPanel>();
        //Could probably just send the data manager if that is the only thing they end up needing
        hud.Initialize(gameSystems);
        shop.Initialize(gameSystems);
        manageFirm.Initialize(gameSystems.DataManager);
        
        

    }
    //Subscribe to UI events here!
    public void SendFirm(FirmController firm)
    {
        DisconnectFirm();

        connectedFirm = firm;

        shop.Initialize(firm);
        hud.Initialize(firm);

        if (connectedFirm == null) return;

        startScreen.GameStarted += connectedFirm.OnGameStarted;
        startScreen.ContinueRequested += OnContinueRequested;   
        connectedFirm.DayEnded += OnDayEnded;
        daySummary.Closed += OnSummaryClosed;
        daySummary.QuitRequested += OnSaveAndQuit;         
        quiz.Finished += OnQuizFinished;
        shop.ClosedAtDayEnd += OnShopClosedAtDayEnd; 

        // the save loaded before the scenes did, so the start screen can now show CONTINUE
        startScreen.ShowSaveOptions(connectedFirm.HasSavedGame,
            $"Saved after day {connectedFirm.CurrentDay} of {connectedFirm.TotalDays}");
    }


    // CONTINUE on the start screen. The researchers and upgrades go back in the lab, then the game carries on
    // from the day summary where it was saved
    private void OnContinueRequested()
    {
        if (connectedFirm == null) return;
        connectedFirm.ContinueSavedGame();
        AfterDaySummary();
    }

    // Closes the data log session, then reloads the Bootstrap scene, which
    // loads the save again and shows the start screen (with CONTINUE). Reloading is the same in the Editor, a desktop
    // build and WebGL: a browser game can't close its own tab, so "quit" means back to the start screen.
    private void OnSaveAndQuit(int day)
    {
        GameLog.EndSession(EndReason.SaveAndQuit);   // DATA LOG: session_end (save_and_quit)
        SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
    }
    //  async, because the mid-game quiz now saves before the next day starts.
    // "Do some saving" was a comment here; this is it
    private async void OnQuizFinished(QuizType type, List<QuizAnswer> answers)
    {
        if (connectedFirm == null) return;
        if (type == QuizType.MidGame)
        {
            // remembered in the save, so carrying on from the day 4 save doesn't ask the quiz again.
            // await: the save has to finish BEFORE NextDay changes the day number
            connectedFirm.MarkMidGameQuizTaken();
            await connectedFirm.SaveGame();
            connectedFirm.NextDay();
        }
        else
        {

            //End the game
            connectedFirm.DeleteSaveInBackground();
        }
    }

    private void OnSummaryClosed(int day)
    {
        AfterDaySummary();
    }

    //  what comes after a day summary. Used by SAVE & CONTINUE and by
    // CONTINUE on the start screen (a save is always made at a day summary)
    private void AfterDaySummary()
    {

        if (connectedFirm== null) return;
        if (connectedFirm.IsMidDay && !connectedFirm.MidGameQuizTaken)
        {
            Debug.Log("Mid Day - place mid survey");
            quiz.Open(QuizType.MidGame);
            
        }

        else if (connectedFirm.IsLastDay)
        {
            // TODO: final survey + leaderboard go here
            Debug.Log("Last day done - final survey would go here");
            quiz.Open(QuizType.PostGame);

        }
        else
        {
            // TODO: open the upgrades screen here once it's done, then start the next day from there
            connectedFirm.NextDay();

        }

    }
    private void OnDayEnded(int day)
    {
        foreach (var screen in screensToClose)
        {
            if (screen != null)
                screen.Close();
        }
        //Close these screens if open!
        codex?.Close();
        manageFirm?.Close();
        quiz?.Cancel();
        
        if (connectedFirm == null || connectedFirm.IsLastDay || shop == null)
        {
            daySummary.Show(day);   // Show this after everything else has closed.
            return;
        }
        summaryDay = day;
        shop.OpenAtDayEnd(day);
        
    }
    
    private void OnShopClosedAtDayEnd()
    {
        if (summaryDay < 0) return;
        int day = summaryDay;
        summaryDay = -1;
        daySummary.Show(day);
    }

    private void DisconnectFirm()
    {
        if (connectedFirm == null) return;

        if (startScreen != null)
        {
            startScreen.GameStarted -= connectedFirm.OnGameStarted;
            startScreen.ContinueRequested -= OnContinueRequested;
        }

        if (daySummary != null)
        {
            daySummary.Closed -= OnSummaryClosed;
            daySummary.QuitRequested -= OnSaveAndQuit; 
        }

        if (quiz != null)
            quiz.Finished -= OnQuizFinished;

        if (shop != null)
            shop.ClosedAtDayEnd -= OnShopClosedAtDayEnd;  

        connectedFirm.DayEnded -= OnDayEnded;
        connectedFirm = null;
    }

    private void OnDestroy()
    {
        DisconnectFirm();
          
    }
}
