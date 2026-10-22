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
        //For testing this is going to be referenced in the savesystem.
        private readonly string _playerId = null;
        private readonly string _apiUrl = "http://localhost:8080";
        //Going to get overwritten anyway
        private readonly int _seed = GameSeed.EnsureInitialized();


        //...
        public DataManager DataManager { get;  }
        public SceneLoader SceneLoader { get; }

        public BaseSaveSystem SaveSystem { get;  }

        

        public GameSystems()
        {
            DataManager = new DataManager();
            SceneLoader = new SceneLoader();
            //Change this concrete implementation change the adapter
            SaveSystem = new SaveSystem(_apiUrl, _playerId, _seed);
        }


    }
}
