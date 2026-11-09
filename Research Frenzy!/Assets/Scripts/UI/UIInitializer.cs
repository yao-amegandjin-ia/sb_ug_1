// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).

using Assets.Scripts.Bootstrap;
using Assets.Scripts.Interfaces;
using GameLogic;
using UI;
using UnityEngine;

public class UIInitializer : MonoBehaviour, ISceneInitializable
{

  

    public GameObject UI;
    public GameObject shopUI;
    
    private GameSystems systems;

    public void InitializeScene(GameSystems gameSystems)
    {
        systems = gameSystems;
        Debug.Log("UI scene initialized");
        // TODO: hand systems to the HUD so it can show money / day
        var hud = UI.GetComponent<HUD>();
        var shop = shopUI.GetComponent<ShopScreen>();
        hud.Initialize(gameSystems);
        shop.Initialize(gameSystems);
        

    }

    public void SendFirm(FirmController firm)
    {
        var hud = UI.GetComponent<HUD>();
        var shop = shopUI.GetComponent<ShopScreen>();
        shop.Initialize(firm);
        hud.Initialize(firm);
    }
}
