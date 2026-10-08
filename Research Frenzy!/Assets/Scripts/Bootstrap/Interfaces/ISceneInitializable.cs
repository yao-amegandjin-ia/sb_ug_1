using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Bootstrap.Interfaces
{

    /// <summary>
    /// Each scene must implement this interface in one component to be added to the scene hierarchy. It defines methods that the persistent systems need to call at different times. 
    /// </summary>
    public interface ISceneInitializable
    {

        //Some initialization that a scene needs on creation. You may or may not want the GameSystems returned. 
        public GameSystems Initialize(GameSystems systems);

       
        
    }
}
