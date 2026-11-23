// ClientLog - the PROCESS events: a client's path through the lab, with timestamps.
//
//   Client enters (wait start)                          -> Entered       (client_enter)
//   Greeted by player (wait stop / greeting start)      -> Greeted       (client_greeted)
//   Matched with researcher (greeting stop / researcher start)   -> Matched (client_matched)
//   Finishes with researcher (researcher stop / post-research wait start) -> ResearchFinished (research_finished)
//   Re-engaged by player (post-research wait stop / walkout start)       -> Reengaged        (client_reengaged)
//   Client leaves (walkout stop)                        -> StudyCollected / TurnedAway / SentHome / Lost (client_leave)
// RESEARCH TIMER: research_finished and client_reengaged used to be placeholders logged at the moment of the
// match. They now have their own functions, called at the real moments (search "RESEARCH TIMER" below).
//
// Plus the steps in between: following the player, the waiting room, opening and closing the match screen.
// The player's answers (right or wrong) are in AnswerLog. Stage tracking is in ClientVisits.

using System.Collections.Generic;
using Clients;
using GameLogic;
using Researchers;

namespace DataLogging
{
    public static class ClientLog
    {
        /// <summary>Client enters (wait starts). FirmController.SpawnNextClient.</summary>
        public static void Entered(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Start(client);
                Dictionary<string, object> data = ClientVisits.Fields(client, v);
                data["is_returning"] = v.VisitNumber > 1;
                data["patience_max"] = LogHelpers.Round(client.MaxPatience);
                GameLog.Emit("process", "client_enter", data);
            });
        }

        /// <summary>
        /// Client greeted by the player (wait stops / greeting starts): the request screen opened for them.
        /// RequestScreen.Open. AnswerLog.QuestionPosted is called right after, since that's when their question shows.
        /// </summary>
        public static void Greeted(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                v.TimesGreeted++;

                Dictionary<string, object> data = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(data, v, ClientVisits.Greeting);
                data["greet_number"] = v.TimesGreeted;
                data["patience_left"] = LogHelpers.Round(client.Patience);
                GameLog.Emit("process", "client_greeted", data);
            });
        }

        /// <summary>"We can help with that": the client follows the player to a researcher. Part of the greeting stage.</summary>
        public static void Following(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                GameLog.Emit("process", "client_following", ClientVisits.Fields(client, ClientVisits.Get(client)));
            });
        }

        /// <summary>"Please wait". seatFound false means every seat was taken, and the client is about to leave.</summary>
        // sent by: ""player" = the player picked "Please wait",
        // "entrance_timeout" = nobody greeted them at the entrance in time, so they went by themselves
        public static void SentToWaiting(ClientData client, bool seatFound, int seatsUsed, int seatsTotal, string sentBy = "player")
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                Dictionary<string, object> data = ClientVisits.Fields(client, v);
                if (seatFound) ClientVisits.ChangeStage(data, v, ClientVisits.WaitingRoom);
                else v.NoSeat = true;
                data["seat_found"] = seatFound;
                data["seats_used"] = seatsUsed;
                data["seats_total"] = seatsTotal;
                data["sent_by"] = sentBy;
                GameLog.Emit("process", "client_sent_to_waiting", data);
            });
        }

        /// <summary>The match screen opened: the player brought the following client to a researcher.</summary>
        public static void MatchScreenOpened(ClientData client, ResearcherData researcher)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                Dictionary<string, object> data = ClientVisits.Fields(client, ClientVisits.Get(client));
                data["researcher_id"] = researcher?.Id;
                data["researcher_name"] = researcher?.Name;
                GameLog.Emit("process", "match_screen_opened", data);
            });
        }

        /// <summary>The match screen closed without a match. how: "no_match" or "close_button".</summary>
        public static void MatchScreenClosed(ClientData client, ResearcherData researcher, string how)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                Dictionary<string, object> data = ClientVisits.Fields(client, ClientVisits.Get(client));
                data["researcher_id"] = researcher?.Id;
                data["how"] = how;
                GameLog.Emit("process", "match_screen_closed", data);
            });
        }

        // RESEARCH TIMER (changed): this used to log client_matched, both placeholders and client_leave all at once.
        // Now it only logs client_matched, and the research session starts. The other three have their own functions below.
        /// <summary>
        /// MATCH pressed (FirmController.StartResearch, after AnswerLog.MatchAnswered). Logs client_matched
        /// (greeting stops / researcher starts). reward is what collecting will pay (after quality),
        /// researchSeconds how long the session lasts (after speed).
        /// </summary>
        public static void Matched(ClientData client, ResearcherData researcher, MatchGrade grade, int reward, float researchSeconds)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);

                Dictionary<string, object> matched = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(matched, v, ClientVisits.WithResearcher);
                matched["researcher_id"] = researcher?.Id;
                // RESEARCH TIMER (new fields)
                matched["researcher_speed"] = researcher?.Speed;
                matched["researcher_quality"] = researcher?.Quality;
                matched["research_seconds"] = LogHelpers.Round(researchSeconds);
                matched["expected_reward"] = reward;
                GameLog.Emit("process", "client_matched", matched);
            });
        }

        // RESEARCH TIMER (new): was a placeholder inside Matched
        /// <summary>
        /// The researcher finished (researcher stops / post-research wait starts). FirmController.HandleResearchFinished.
        /// endedBy: "timer" (the session ran its full length) or "day_end" (the day ended first, so it was cut short).
        /// </summary>
        public static void ResearchFinished(ClientData client, ResearcherData researcher, string endedBy)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                Dictionary<string, object> finished = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(finished, v, ClientVisits.PostResearchWait);
                finished["researcher_id"] = researcher?.Id;
                finished["ended_by"] = endedBy;
                finished["research_seconds_planned"] = LogHelpers.Round(client.ResearchDuration);
                GameLog.Emit("process", "research_finished", finished);
            });
        }

        // RESEARCH TIMER (new): was a placeholder inside Matched
        /// <summary>
        /// The player clicked the client to collect the result (post-research wait stops / walkout starts).
        /// FirmController.CollectResearch. autoCollected is true when the day ended and the game collected it instead.
        /// </summary>
        public static void Reengaged(ClientData client, bool autoCollected)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                Dictionary<string, object> reengaged = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(reengaged, v, ClientVisits.Walkout);
                reengaged["auto_collected_at_day_end"] = autoCollected;
                GameLog.Emit("process", "client_reengaged", reengaged);
            });
        }

        // RESEARCH TIMER (new): the client_leave part of the old Matched
        /// <summary>The client leaves with their result (walkout stops). FirmController.CollectResearch, right after Reengaged.</summary>
        public static void StudyCollected(ClientData client, MatchGrade grade, int reward)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                Leave(client, ClientVisits.Get(client), "study_complete", ClientResult.FromStudy(grade.Method).ToString(), reward);
            });
        }

        /// <summary>"We can't help": the client is turned away and leaves. FirmController.RejectRequest.</summary>
        public static void TurnedAway(ClientData client, bool wasCorrect, int reward)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                string outcome = (wasCorrect ? ClientOutcome.CorrectReject : ClientOutcome.WrongReject).ToString();
                Leave(client, ClientVisits.Get(client), "turned_away", outcome, reward);
            });
        }

        /// <summary>"Come back tomorrow": the client leaves and comes back the next day (as a new visit).</summary>
        public static void SentHome(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                Leave(client, ClientVisits.Get(client), "return_tomorrow", "Returning", 0);
            });
        }

        /// <summary>The client gave up: patience ran out, or they were asked to wait with no seat free.</summary>
        public static void Lost(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                string reason = v.NoSeat ? "no_seat_in_waiting_room" : "patience_ran_out";
                Leave(client, v, reason, ClientOutcome.Lost.ToString(), 0);
            });
        }

        // client_leave: the end of every visit that isn't interrupted
        private static void Leave(ClientData client, ClientVisit v, string reason, string outcome, int reward)
        {
            Dictionary<string, object> data = ClientVisits.Fields(client, v);
            ClientVisits.ChangeStage(data, v, ClientVisits.Left);
            data["leave_reason"] = reason;
            data["outcome"] = outcome;
            data["reward"] = reward;
            data["time_in_lab_seconds"] = LogHelpers.Round(LogHelpers.Now - v.EnterTime);
            GameLog.Emit("process", "client_leave", data);
            ClientVisits.Remove(client);
        }
    }
}
