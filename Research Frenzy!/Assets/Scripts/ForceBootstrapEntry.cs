using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class ForceBootstrapEntry 
{
    //Static constructors called before any instance of a class is created and called only once. 
    static ForceBootstrapEntry()
    {
        // Path to your bootstrap scene asset
        var path = "Assets/Scenes/Bootstrap.unity";
        SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorSceneManager.playModeStartScene = bootstrap;
    }
}
