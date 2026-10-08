using Assets.Scripts.Bootstrap;
using Assets.Scripts.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public async Awaitable<(ISceneInitializable first, ISceneInitializable second)> AddScenes()
    {
        var componentList = new List<ISceneInitializable>();
        string name = "Research_Lab";
        Scene labScene;
        Scene uiScene;
        await SceneManager.LoadSceneAsync
            (
            name,
            LoadSceneMode.Additive
            );
        labScene = SceneManager.GetSceneByName(name);
        if (labScene.IsValid())
        {
            componentList.Add(findInterfaceInScene(labScene));

            
        }
        
     

        name = "UI";
        await SceneManager.LoadSceneAsync
            (
            name,
            LoadSceneMode.Additive
            );

        uiScene = SceneManager.GetSceneByName(name);
        if (uiScene.IsValid())
        {
            componentList.Add(findInterfaceInScene(uiScene));
        }
        

        SceneManager.SetActiveScene(labScene);
    }

    private ISceneInitializable findInterfaceInScene(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        return rootObjects.Select(root => root.GetComponentInChildren<ISceneInitializable>(true)).FirstOrDefault();

    }




}
