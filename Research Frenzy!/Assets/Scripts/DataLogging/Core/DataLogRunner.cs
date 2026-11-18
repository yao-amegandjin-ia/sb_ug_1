// DataLogRunner - an invisible GameObject that GameLog creates by itself when a session starts.
// You don't add it to a scene. It's kept between scene loads (DontDestroyOnLoad) and does three jobs that
// need a MonoBehaviour:
//   1. every frame: lets the sink save what's been written (at most every 2 seconds in WebGL)
//   2. Editor / desktop: closing the game (or pressing Stop in the Editor) ends the session in the log
//   3. WebGL: hears the tab being hidden, shown again or closed, through DataLogging.jslib

using UnityEngine;

namespace DataLogging
{
    public class DataLogRunner : MonoBehaviour
    {
        private static DataLogRunner instance;

        public static void EnsureExists()
        {
            if (instance != null) return;
            GameObject go = new GameObject("DataLogRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DataLogRunner>();
            DataLogWebGL.RegisterPageEvents(GameLog.OnPageEvent);
        }

        private void Update()
        {
            GameLog.Tick();
        }

        // runs in the Editor and desktop builds. NOT in a browser tab (the page events cover that)
        private void OnApplicationQuit()
        {
            GameLog.EndSession(EndReason.AppQuit);
        }
    }
}
