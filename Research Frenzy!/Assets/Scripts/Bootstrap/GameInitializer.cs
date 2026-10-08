using Assets.Scripts.Bootstrap;
using UnityEngine;

public class GameInitializer : MonoBehaviour
{

    

    //Not a singleton but this is the reference to the systems and you should never make another one of these. Use dependency injection instead in this scene. 
    //If working on other scenes I would create an intializer script for your scene and access this statically but still use dependency injection so that we do not 
    //just flippantly use the global access all the time. 
    public static GameSystems Systems;

    private void Awake()
    {
        Systems = new GameSystems();
        
    }

    private async Awaitable Start()
    {
        await Systems.SceneLoader.AddScenes();

    }

}
