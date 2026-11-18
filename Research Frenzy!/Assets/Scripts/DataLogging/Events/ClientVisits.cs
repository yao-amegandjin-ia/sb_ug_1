// ClientVisits - keeps track of every client currently in the lab, for the log.
// Each time a client walks in is one "visit" with its own visit_id. A visit moves through stages
// (waiting at the door, greeting, with the researcher...), and this remembers which stage each one is in
// and when it started, so every event can say how long the last stage took.
// ClientLog and AnswerLog both use it. It also writes client_interrupted for visits that get cut off.

using System.Collections.Generic;
using System.Linq;
using Clients;

namespace DataLogging
{
    /// <summary>One client's visit to the lab, from walking in to leaving.</summary>
    internal class ClientVisit
    {
        public int VisitId;
        public int VisitNumber;            // 1 the first time this request's client comes in this session, 2 if they return...
        public string Stage;
        public double StageStart;
        public double EnterTime;
        public int TimesGreeted;
        public double QuestionPostedAt = -1;
        public bool NoSeat;                // asked to wait with every seat taken. They're about to leave
    }

    internal static class ClientVisits
    {
        // the stages of a visit, in order. The guide maps these onto the process steps
        public const string WaitingAtDoor = "waiting_at_door";   // entered, waiting to be greeted
        public const string Greeting = "greeting";               // the player is talking to them / walking them over
        public const string WaitingRoom = "waiting_room";        // sent to sit down, waiting to be greeted again
        public const string WithResearcher = "with_researcher";  // matched, the researcher is working
        public const string PostResearchWait = "post_research_wait";
        public const string Walkout = "walkout";                 // leaving the lab
        public const string Left = "left";

        // keyed by the ClientData object itself, since the same object stays with the client the whole visit
        private static readonly Dictionary<ClientData, ClientVisit> open = new Dictionary<ClientData, ClientVisit>();
        private static readonly Dictionary<int, int> visitsPerRequest = new Dictionary<int, int>();
        private static int nextVisitId = 1;

        public static void Clear()
        {
            open.Clear();
            visitsPerRequest.Clear();
        }

        /// <summary>A new visit, starting at the door.</summary>
        public static ClientVisit Start(ClientData client)
        {
            visitsPerRequest.TryGetValue(client.RequestId, out int count);
            visitsPerRequest[client.RequestId] = count + 1;

            ClientVisit v = new ClientVisit
            {
                VisitId = nextVisitId++,
                VisitNumber = count + 1,
                Stage = WaitingAtDoor,
                StageStart = LogHelpers.Now,
                EnterTime = LogHelpers.Now,
            };
            open[client] = v;
            return v;
        }

        /// <summary>
        /// This client's visit. If the log never saw them enter (e.g. an interruption cleared the list while
        /// they stayed in the lab), a new visit is started and a client_untracked line says so.
        /// </summary>
        public static ClientVisit Get(ClientData client)
        {
            if (open.TryGetValue(client, out ClientVisit v)) return v;
            v = Start(client);
            GameLog.Emit("process", "client_untracked", Fields(client, v));
            return v;
        }

        /// <summary>The visit is over (the client left).</summary>
        public static void Remove(ClientData client)
        {
            open.Remove(client);
        }

        /// <summary>The fields every process event starts with.</summary>
        public static Dictionary<string, object> Fields(ClientData client, ClientVisit v)
        {
            return new Dictionary<string, object>
            {
                ["visit_id"] = v.VisitId,
                ["client_id"] = client.Id,
                ["request_id"] = client.RequestId,
                ["client_name"] = client.Name,
                ["visit_number"] = v.VisitNumber,
                ["stage"] = v.Stage,
            };
        }

        /// <summary>Moves the visit to a new stage, and adds which stage ended and how long it lasted to data.</summary>
        public static void ChangeStage(Dictionary<string, object> data, ClientVisit v, string newStage)
        {
            data["previous_stage"] = v.Stage;
            data["previous_stage_seconds"] = LogHelpers.Round(LogHelpers.Now - v.StageStart);
            v.Stage = newStage;
            v.StageStart = LogHelpers.Now;
            data["stage"] = newStage;
        }

        /// <summary>Every client still in the lab, for day_end.</summary>
        public static List<object> Snapshot()
        {
            return open.Select(kv => (object)new Dictionary<string, object>
            {
                ["visit_id"] = kv.Value.VisitId,
                ["client_id"] = kv.Key.Id,
                ["request_id"] = kv.Key.RequestId,
                ["stage"] = kv.Value.Stage,
                ["stage_seconds"] = LogHelpers.Round(LogHelpers.Now - kv.Value.StageStart),
            }).ToList();
        }

        /// <summary>Logs client_interrupted for every open visit, then forgets them all.</summary>
        public static void InterruptAll(string reason)
        {
            foreach (KeyValuePair<ClientData, ClientVisit> kv in open.ToList())
            {
                Dictionary<string, object> data = Fields(kv.Key, kv.Value);
                data["interrupted_by"] = reason;
                data["stage_seconds"] = LogHelpers.Round(LogHelpers.Now - kv.Value.StageStart);
                data["time_in_lab_seconds"] = LogHelpers.Round(LogHelpers.Now - kv.Value.EnterTime);
                data["patience_left"] = LogHelpers.Round(kv.Key.Patience);
                GameLog.Emit("process", "client_interrupted", data);
            }
            open.Clear();
        }
    }
}
