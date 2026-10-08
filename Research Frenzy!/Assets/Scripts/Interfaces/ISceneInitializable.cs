using Assets.Scripts.Bootstrap;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Interfaces
{
    /// <summary>
    /// If this interface is not implemented by a scene, you will not get access to the persistent game systems...so implement it.
    /// </summary>
    public interface ISceneInitializable
    {
        /// <summary>
        /// This method should handle the initialization of dependencies for any systems you create on the scene level. Provided is the GameSystems object for accessing persisten systems. 
        /// </summary>
        public void InitializeScene(GameSystems gameSystems);
    }
}
