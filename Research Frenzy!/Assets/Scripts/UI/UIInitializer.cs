// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).

using Bootstrap;
using GameLogic;
using Interfaces;
using UI;
using UI.HUDScreens;
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
        // (the day timer is FirmController's own now, so only the start button needs wiring here)
        if (connectedFirm != null)
        {
            startScreen.GameStarted -= connectedFirm.OnGameStarted;
        }

        connectedFirm = firm;

        if (connectedFirm != null)
        {
            startScreen.GameStarted += firm.OnGameStarted;

        }


        shop.Initialize(firm);
        hud.Initialize(firm);
    }


    private void OnDestroy()
    {
        if (startScreen != null && connectedFirm != null)
        {
            startScreen.GameStarted -= connectedFirm.OnGameStarted;
        }
            


    }
}