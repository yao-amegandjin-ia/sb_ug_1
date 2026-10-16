using UnityEngine;
using Researchers;

/// <summary>
///
/// Prefab component for researcher.
/// This holds no data or logic, ResearcherData.cs holds all of that.
/// This component displays what is in ResearcherData.cs.
/// The prefab is the same for all researchers.
/// Bind() applies the ResearcherData to this object, making each Researcher different.
/// </summary>

public class Researcher : MonoBehaviour
{
    // Shown in the Inspector so you can watch stats at runtime. It points at the same
    // object the hiring UI and save system use, so changes made in code show up here.
    
    //This is shown in the Inspector so we can see stats during runtime.
    //Points to the same object
    [SerializeField] private ResearcherData data;
    
    // Optional: sprite layers for the look. Arrays are indexed by ResearcherData.Appearance
    // (x: head, y: torso, z: legs). Leave empty if you set the look some other way.
    [Header("Appearance (optional)")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private SpriteRenderer torsoRenderer;
    [SerializeField] private SpriteRenderer legsRenderer;
    [SerializeField] private Sprite[] headSprites;
    [SerializeField] private Sprite[] torsoSprites;
    [SerializeField] private Sprite[] legSprites;
    
    public ResearcherData Data => data;

    /// <summary>Makes this object represent the given researcher. Call right after instantiating.</summary>
    public void Bind(ResearcherData researcherData)
    {
        data = researcherData;
        gameObject.name = data.Name;
        ApplyAppearance();
    }
    
    //This sets the sprites to render for the respective parts.
    //Appearance is a Vector3Int, x = head, y = torso, z = leg
    private void ApplyAppearance()
    {
        Vector3Int look = data.Appearance;
        SetSprite(headRenderer, headSprites, look.x);
        SetSprite(torsoRenderer, torsoSprites, look.y);
        SetSprite(legsRenderer, legSprites, look.z);
    }
 
    // Does nothing if the renderer or sprite list isn't set up, so a bare prefab still works.
    private static void SetSprite(SpriteRenderer target, Sprite[] sprites, int index)
    {
        if (target == null || sprites == null || index < 0 || index >= sprites.Length) return;
        target.sprite = sprites[index];
    }


    
}