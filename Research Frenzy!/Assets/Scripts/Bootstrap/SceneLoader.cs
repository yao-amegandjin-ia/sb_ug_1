using Assets.Scripts.Bootstrap;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This gameobject handles additively loading scenes on startup. 
/// </summary>
public class SceneLoader 
{

   

    //Loading the unity scene takes time so we use async to let thread work on other things while waiting. 
    //Add logging to this

    //Note will need to figure out fix for the no camera rendering later. 

    /// <summary>
    /// Add scenes primary job is to Load scenes additively and initialize those scenes. 
    /// </summary>
    /// <returns></returns>
    public async Awaitable AddScenes()
    {
        
        string name = "Research_Lab";
        await SceneManager.LoadSceneAsync
            (
            name,
            LoadSceneMode.Additive
            );

     

        name = "UI";
        await SceneManager.LoadSceneAsync
            (
            name,
            LoadSceneMode.Additive
            );


        Scene labScene =
            SceneManager.GetSceneByName("Research_Lab");

        SceneManager.SetActiveScene(labScene);
    }



   

   
}
