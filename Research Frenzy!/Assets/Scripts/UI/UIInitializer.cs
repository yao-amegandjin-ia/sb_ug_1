// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).
//   day ends -> shop (upgrades + hire) -> DAY SUMMARY button -> day summary -> Continue -> (day 4: mid-game quiz) -> next day.
// The shop's close button opens the summary (ShopScreen.ClosedAtDayEnd -> OnShopClosedAtDayEnd).
// After the last day there's no shop: the summary opens straight away, then the final quiz.

using Bootstrap;
using GameLogic;
using Interfaces;
using System.Collections.Generic;
using UI;
using UI.ClientScreens;
using UI.HUDScreens;
using UnityEngine;
using UnityEngine.InputSystem.HID;

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
        connectedFirm.DayEnded += OnDayEnded;
        daySummary.Closed += OnSummaryClosed;
        quiz.Finished += OnQuizFinished;
        shop.ClosedAtDayEnd += OnShopClosedAtDayEnd; 
    }
    private void OnQuizFinished(QuizType type, List<QuizAnswer> answers)
    {
        if (type == QuizType.MidGame)
            //Do some saving
            connectedFirm.NextDay();
        else
        {

            //End the game
            
        }
    }
    private void OnSummaryClosed(int day)
    {

        if (connectedFirm== null) return;
        if (connectedFirm.IsMidDay)
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
            startScreen.GameStarted -= connectedFirm.OnGameStarted;

        if (daySummary != null)
            daySummary.Closed -= OnSummaryClosed;

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