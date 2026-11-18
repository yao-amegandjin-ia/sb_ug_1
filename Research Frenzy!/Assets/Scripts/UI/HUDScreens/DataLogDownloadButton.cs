// DataLogDownloadButton - OPTIONAL. Add it to the GameUI object (next to HUD, QuizScreen...) to get a
// "Download Logs" button under the other demo buttons. In a WebGL build it downloads every session's log
// as one .jsonl file. In the Editor it prints where the DataLogs folder is.
// Handy for playtests while there's no server collecting the logs. Remove it (or untick showButton) for real players.

using DataLogging;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.HUDScreens
{
    public class DataLogDownloadButton : MonoBehaviour, IGameUIPart
    {
        public bool showButton = true;
        public bool allSessions = true;

        // called by GameUIPanel once the UI has loaded (and again if it reloads)
        public void Bind(VisualElement root)
        {
            if (!showButton) return;

            Button button = new Button(() => LogDownload.DownloadLogs(allSessions));
            button.text = "Download Logs";
            button.style.position = Position.Absolute;
            button.style.top = 160;   // under QuizScreen's Mid Quiz (88) and Post Quiz (124) buttons
            button.style.right = 16;
            root.Add(button);
        }
    }
}
