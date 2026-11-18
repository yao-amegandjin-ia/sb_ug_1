// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).

using Bootstrap;
using GameLogic;
using Interfaces;
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
    
    public GameObject UI;
    public GameObject shopUI;

    private FirmController connectedFirm;
    private HUD hud;

    private ShopScreen shop;
    private StartScreen startScreen;
    private GameSystems systems;

    // the persistent systems, once the bootstrap has handed them over. null before that
    public GameSystems Systems => systems;

    public void InitializeScene(GameSystems gameSystems)
    {
        systems = gameSystems;
        Debug.Log("UI scene initialized");
        daySummary = UI.GetComponent<DaySummary>();
        codex = UI.GetComponent<CodexPanel>();
        
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
        
        // Show this after everything else has closed.
        daySummary.Show(day);
    }

    private void DisconnectFirm()
    {
        if (connectedFirm == null) return;

        if (startScreen != null)
            startScreen.GameStarted -= connectedFirm.OnGameStarted;

        connectedFirm.DayEnded -= OnDayEnded;
        connectedFirm = null;
    }

    private void OnDestroy()
    {
        DisconnectFirm();
          
    }
}