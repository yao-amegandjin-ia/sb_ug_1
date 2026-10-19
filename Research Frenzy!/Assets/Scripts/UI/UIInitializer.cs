// UIInitializer - goes on the UIRoot object in the UI scene.
// the bootstrap SceneLoader looks for one ISceneInitializable per scene and calls InitializeScene on it,
// so this is where the UI gets access to the game systems (data manager etc).

using Assets.Scripts.Bootstrap;
using Assets.Scripts.Interfaces;
using UnityEngine;

public class UIInitializer : MonoBehaviour, ISceneInitializable
{
    public StartScreen startScreen;

    GameSystems systems;

    public void InitializeScene(GameSystems gameSystems)
    {
        systems = gameSystems;
        Debug.Log("UI scene initialized");
        // TODO: hand systems to the HUD so it can show money / day
    }
}
