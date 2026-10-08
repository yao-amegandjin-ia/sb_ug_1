using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Bootstrap
{
    public class GameSystems
    {
        //...
        public DataManager DataManager { get;  }
        public SceneLoader SceneLoader { get; }

        public GameSystems(SceneLoader sceneLoader)
        {
            DataManager = new DataManager();
            SceneLoader = sceneLoader;
        }


    }
}
