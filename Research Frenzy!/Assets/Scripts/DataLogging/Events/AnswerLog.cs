// AnswerLog - the LEARNING / PERFORMANCE events: what the player answered about each client, what the right
// answer was, whether they got it right, and how long it took. The "question" is the client's request, and
// question_id is the request ID from the requests spreadsheet.
//
//   question posted            -> QuestionPosted    (question_posted)
//   "can we help?" answered    -> QuestionAnswered  (question_answered: correct answer, player response, accuracy, time)
//   step 1 approach picked     -> ApproachChosen    (approach_chosen)
//   MATCH                      -> MatchAnswered     (match_answered: possible correct researchers, actual pick, accuracy)
//
// These sit in between ClientLog's process events in the log (same visit_id).

using System.Collections.Generic;
using System.Linq;
using Clients;
using GameDataObjects;
using GameLogic;
using Researchers;

namespace DataLogging
{
    public static class AnswerLog
    {
        /// <summary>The client's question is shown to the player. RequestScreen.Open, right after ClientLog.Greeted.</summary>
        public static void QuestionPosted(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                v.QuestionPostedAt = LogHelpers.Now;
                GameLog.Emit("learning", "question_posted", new Dictionary<string, object>
                {
                    ["question_id"] = client.RequestId,
                    ["visit_id"] = v.VisitId,
                    ["client_id"] = client.Id,
                    ["times_posted"] = v.TimesGreeted,
                    ["question_text"] = client.Request?.ProblemText,
                });
            });
        }

        /// <summary>"We can help" (true) or "We can't help" (false) on the request screen.</summary>
        public static void QuestionAnswered(ClientData client, bool playerSaysCanHelp)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                bool researchable = client.Request != null && client.Request.IsResearchable;

                GameLog.Emit("learning", "question_answered", new Dictionary<string, object>
                {
                    ["question_id"] = client.RequestId,
                    ["visit_id"] = v.VisitId,
                    ["client_id"] = client.Id,
                    ["correct_answer"] = researchable ? "can_help" : "cannot_help",
                    ["player_response"] = playerSaysCanHelp ? "can_help" : "cannot_help",
                    ["is_correct"] = playerSaysCanHelp == researchable,
                    ["response_seconds"] = LogHelpers.SecondsSince(v.QuestionPostedAt),
                });
            });
        }

        /// <summary>The request screen was closed with X, without answering.</summary>
        public static void QuestionDismissed(ClientData client)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                GameLog.Emit("learning", "question_dismissed", new Dictionary<string, object>
                {
                    ["question_id"] = client.RequestId,
                    ["visit_id"] = v.VisitId,
                    ["client_id"] = client.Id,
                    ["seconds_open"] = LogHelpers.SecondsSince(v.QuestionPostedAt),
                });
            });
        }

        /// <summary>Step one of matching: the player picked an approach and pressed NEXT.</summary>
        public static void ApproachChosen(ClientData client, ResearcherData researcher, ResearchApproach approach)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                RequestData r = client.Request;
                GameLog.Emit("learning", "approach_chosen", new Dictionary<string, object>
                {
                    ["question_id"] = client.RequestId,
                    ["visit_id"] = ClientVisits.Get(client).VisitId,
                    ["researcher_id"] = researcher?.Id,
                    ["player_approach"] = approach.ToString(),
                    ["correct_approach"] = CorrectApproach(r),
                    ["is_correct"] = StudyScoring.IsCorrectApproach(r, approach),
                });
            });
        }

        /// <summary>
        /// MATCH pressed (FirmController.CompleteStudy). Logs every correct option, what the player picked, and how right it was.
        /// </summary>
        /// <param name="possibleResearchers">From FindCorrectResearchers, worked out BEFORE the client is assigned.</param>
        public static void MatchAnswered(ClientData client, ResearcherData researcher, ResearchApproach approach,
            int methodologyId, MatchGrade grade, int reward, List<object> possibleResearchers)
        {
            GameLog.Safe(() =>
            {
                if (client == null) return;
                ClientVisit v = ClientVisits.Get(client);
                RequestData r = client.Request;

                GameLog.Emit("learning", "match_answered", new Dictionary<string, object>
                {
                    ["question_id"] = client.RequestId,
                    ["visit_id"] = v.VisitId,
                    ["client_id"] = client.Id,
                    // what was right
                    ["correct_approach"] = CorrectApproach(r),
                    ["best_methodology_id"] = r?.BestMethodologyId ?? 0,
                    ["best_methodology_name"] = r != null ? MethodLibrary.GetName(r.BestMethodologyId) : null,
                    ["good_methodology_ids"] = r?.GoodMethodologyIds ?? new List<int>(),
                    ["possible_correct_researchers"] = possibleResearchers ?? new List<object>(),
                    // what the player did
                    ["player_researcher_id"] = researcher?.Id,
                    ["player_researcher_name"] = researcher?.Name,
                    ["player_approach"] = approach.ToString(),
                    ["player_methodology_id"] = methodologyId,
                    ["player_methodology_name"] = MethodLibrary.GetName(methodologyId),
                    // how right it was
                    ["approach_correct"] = grade.ApproachCorrect,
                    ["methodology_grade"] = grade.Method.ToString().ToLowerInvariant(),   // best / good / wrong
                    ["researcher_fit"] = ResearcherFit(researcher, possibleResearchers),  // best / good / none
                    ["reward"] = reward,
                    ["seconds_since_question_posted"] = LogHelpers.SecondsSince(v.QuestionPostedAt),
                });
            });
        }

        /// <summary>
        /// Every hired researcher who could correctly take this request: "best" if one of their usable methods is the
        /// request's best methodology, "good" if one is a partial-credit methodology. Empty for unresearchable requests.
        /// </summary>
        public static List<object> FindCorrectResearchers(RequestData request, IEnumerable<ResearcherData> hired, int unlockedMethods)
        {
            List<object> result = new List<object>();
            if (request == null || !request.IsResearchable || hired == null) return result;

            foreach (ResearcherData researcher in hired)
            {
                if (researcher == null) continue;
                List<int> usable = researcher.KnownMethodologyIds.Take(unlockedMethods).ToList();

                string fit = null;
                int via = 0;
                if (usable.Contains(request.BestMethodologyId))
                {
                    fit = "best";
                    via = request.BestMethodologyId;
                }
                else if (request.GoodMethodologyIds != null)
                {
                    foreach (int id in usable)
                        if (request.GoodMethodologyIds.Contains(id)) { fit = "good"; via = id; break; }
                }

                if (fit == null) continue;
                result.Add(new Dictionary<string, object>
                {
                    ["researcher_id"] = researcher.Id,
                    ["researcher_name"] = researcher.Name,
                    ["fit"] = fit,
                    ["methodology_id"] = via,
                    ["was_available"] = researcher.IsAvailable,
                });
            }
            return result;
        }

        // ---------- helpers ----------

        private static string CorrectApproach(RequestData r)
        {
            return r != null && r.IsResearchable ? r.Approach.ToString() : "None";
        }

        // how the picked researcher compares with the list of correct ones
        private static string ResearcherFit(ResearcherData researcher, List<object> possibleResearchers)
        {
            if (researcher == null || possibleResearchers == null) return "none";
            foreach (object o in possibleResearchers)
                if (o is Dictionary<string, object> d && Equals(d["researcher_id"], researcher.Id))
                    return (string)d["fit"];
            return "none";
        }
    }
}
