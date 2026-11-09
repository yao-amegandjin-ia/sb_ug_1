// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).

using Assets.Scripts.Bootstrap;
using Assets.Scripts.Interfaces;
using GameLogic;
using UI;
using UnityEngine;
using UnityEngine.InputSystem.HID;

public class UIInitializer : MonoBehaviour, ISceneInitializable
{

  

    public GameObject UI;
    public GameObject shopUI;

    private FirmController connectedFirm;
    private HUD hud;

    private ShopScreen shop;
    public StartScreen startScreen;
    private GameSystems systems;

    // the persistent systems, once the bootstrap has handed them over. null before that
    public GameSystems Systems => systems;

    public void InitializeScene(GameSystems gameSystems)
    {
        systems = gameSystems;
        Debug.Log("UI scene initialized");
        hud = UI.GetComponent<HUD>();
        shop = shopUI.GetComponent<ShopScreen>();
        startScreen = UI.GetComponent<StartScreen>();
        hud.Initialize(gameSystems);
        shop.Initialize(gameSystems);
        

    }
    //Subscribe to UI events here!
    public void SendFirm(FirmController firm)
    {
        

        //Unsubscribe to event if sendfirm called multiple times!
        if (connectedFirm != null)
        {
            startScreen.GameStarted -= firm.OnGameStarted;
            hud.DayEnded -= firm.OnDayEnded;
            firm.DayEnded -= hud.EndDay;

        }

        connectedFirm = firm;

        if (connectedFirm != null)
        {
            startScreen.GameStarted += firm.OnGameStarted;
            hud.DayEnded += firm.OnDayEnded;
            firm.DayEnded += hud.EndDay;

        }


        shop.Initialize(firm);
        hud.Initialize(firm);
    }


    private void OnDestroy()
    {
        if (startScreen != null && connectedFirm != null)
        {
            startScreen.GameStarted -= connectedFirm.OnGameStarted;
            hud.DayEnded -= connectedFirm.OnDayEnded;
            connectedFirm.DayEnded -= hud.EndDay;
        }
            


    }
}
