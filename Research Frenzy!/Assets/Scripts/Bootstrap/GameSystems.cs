using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Bootstrap
{
    /// <summary>
    /// Container for persistent game systems 
    /// </summary>
    public class GameSystems
    {
        
        //...
        public DataManager DataManager { get;  }
        public  SceneLoader SceneLoader { get; }
        

        

        public GameSystems(BaseSaveSystem saveSystem)
        {
            DataManager = new DataManager(saveSystem);
            SceneLoader = new SceneLoader();
            
            
        }


    }
}
