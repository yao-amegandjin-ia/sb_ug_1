using Assets.Scripts.Bootstrap;
using UnityEngine;

public class GameInitializer : MonoBehaviour
{

    [SerializeField] private SceneLoader sceneLoader;

    //Accessing persistent systems
    public static GameSystems Systems { get; private set; }

    private void Awake()
    {
        Systems = new GameSystems(sceneLoader);
    }
}
