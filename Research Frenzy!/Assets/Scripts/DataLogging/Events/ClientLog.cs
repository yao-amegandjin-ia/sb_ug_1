// ClientLog - the PROCESS events: a client's path through the lab, with timestamps.
//
//   Client enters (wait start)                          -> Entered       (client_enter)
//   Greeted by player (wait stop / greeting start)      -> Greeted       (client_greeted)
//   Matched with researcher (greeting stop / researcher start)   -> Matched (client_matched)
//   Finishes with researcher (researcher stop / post-research wait start) -> Matched (research_finished, placeholder)
//   Re-engaged by player (post-research wait stop / walkout start)       -> Matched (client_reengaged, placeholder)
//   Client leaves (walkout stop)                        -> Matched / TurnedAway / SentHome / Lost (client_leave)
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
        public static void SentToWaiting(ClientData client, bool seatFound, int seatsUsed, int seatsTotal)
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

        /// <summary>
        /// MATCH pressed (FirmController.CompleteStudy, after AnswerLog.MatchAnswered). Logs client_matched
        /// (greeting stops / researcher starts), then the two placeholders below, then client_leave.
        /// </summary>
        public static void Matched(ClientData client, ResearcherData researcher, MatchGrade grade, int reward)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);

                Dictionary<string, object> matched = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(matched, v, ClientVisits.WithResearcher);
                matched["researcher_id"] = researcher?.Id;
                GameLog.Emit("process", "client_matched", matched);

                // ---- PLACEHOLDERS ----
                // The game has no research timer yet: the client leaves the moment they're matched.
                // So "researcher finishes" and "player re-engages the client" happen at the same instant and are
                // marked placeholder = true. When a research timer is added, move these two into their own functions,
                // call them where the researcher really finishes (e.g. ResearcherData.AssignmentFinished) and where the
                // player clicks the client again, and drop the placeholder flag.
                Dictionary<string, object> finished = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(finished, v, ClientVisits.PostResearchWait);
                finished["researcher_id"] = researcher?.Id;
                finished["placeholder"] = true;
                GameLog.Emit("process", "research_finished", finished);

                Dictionary<string, object> reengaged = ClientVisits.Fields(client, v);
                ClientVisits.ChangeStage(reengaged, v, ClientVisits.Walkout);
                reengaged["placeholder"] = true;
                GameLog.Emit("process", "client_reengaged", reengaged);

                Leave(client, v, "study_complete", ClientResult.FromStudy(grade.Method).ToString(), reward);
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
