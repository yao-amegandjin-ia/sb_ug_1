using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    //Loading the unity scene takes time so we use async to let thread work on other things while waiting. 
    //Add logging to this

    //Note will need to figure out fix for the no camera rendering later. 
    private async Awaitable Start()
    {

        await SceneManager.LoadSceneAsync
            (
            "Research_Lab",
            LoadSceneMode.Additive
            );

        await SceneManager.LoadSceneAsync
            (
            "UI",
            LoadSceneMode.Additive
            );


        Scene labScene =
            SceneManager.GetSceneByName("Research_Lab");

        SceneManager.SetActiveScene(labScene);

    }
  

   
}
