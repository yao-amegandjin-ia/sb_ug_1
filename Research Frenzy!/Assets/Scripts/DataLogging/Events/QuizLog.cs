// QuizLog - the QUIZ events: the mid and post quizzes, overall and per question. Called from QuizScreen.
//
//   quiz opens               -> Started          (quiz_start: question order, attempt counts) + first quiz_question_start
//   a question appears       -> QuestionShown    (quiz_question_start)
//   the player picks         -> QuestionAnswered (quiz_question_answer: start/stop, correct answer, response, accuracy)
//   last question answered   -> Finished         (quiz_end: start/stop/duration, score)
//   cut off                  -> Interrupt        (quiz_interrupted, called by GameLog)

using System.Collections.Generic;
using System.Linq;
using Interfaces;
using UI.HUDScreens;

namespace DataLogging
{
    public static class QuizLog
    {
        // the quiz in progress, null when none is open
        private class QuizRun
        {
            public QuizType Type;
            public int AttemptThisSession;
            public int AttemptAllTime;
            public List<int> Sequence;
            public double Start;
            public string StartTs;
            public int Position = -1;          // index of the question on screen
            public int QuestionId;
            public double QuestionStart;
            public string QuestionStartTs;
            public int Answered;
            public int Correct;
        }

        private static QuizRun quiz;
        private static readonly Dictionary<QuizType, int> attemptsThisSession = new Dictionary<QuizType, int>();

        /// <summary>A new session: forget the old quiz and counts. Called by GameLog.StartSession.</summary>
        internal static void Reset()
        {
            quiz = null;
            attemptsThisSession.Clear();
        }

        /// <summary>A quiz opened. Logs quiz_start (with the question order) and the first quiz_question_start.</summary>
        public static void Started(QuizType type, IList<QuizQuestion> questions)
        {
            GameLog.Safe(() =>
            {
                if (questions == null || questions.Count == 0) return;
                if (quiz != null) Interrupt("quiz_restarted");

                // how many times this quiz has been taken: in this session, and ever on this device (kept in LoggerState)
                attemptsThisSession.TryGetValue(type, out int thisSession);
                thisSession++;
                attemptsThisSession[type] = thisSession;

                int allTime = thisSession;
                LoggerState state = GameLog.State;
                if (state != null)
                {
                    state.QuizAttemptsAllTime.TryGetValue(type.ToString(), out allTime);
                    allTime++;
                    state.QuizAttemptsAllTime[type.ToString()] = allTime;
                    state.Save();
                }

                quiz = new QuizRun
                {
                    Type = type,
                    AttemptThisSession = thisSession,
                    AttemptAllTime = allTime,
                    Sequence = questions.Select(q => q.id).ToList(),
                    Start = LogHelpers.Now,
                    StartTs = LogHelpers.IsoNow(),
                };

                GameLog.Emit("quiz", "quiz_start", new Dictionary<string, object>
                {
                    ["quiz_type"] = Name(type),
                    ["attempt_this_session"] = thisSession,
                    ["attempt_all_time"] = allTime,
                    ["question_sequence"] = quiz.Sequence,
                    ["question_count"] = quiz.Sequence.Count,
                    ["start_ts"] = quiz.StartTs,
                });

                QuestionShown(questions[0], 0);
            });
        }

        /// <summary>A quiz question appeared. Its start time is also the previous question's stop time.</summary>
        public static void QuestionShown(QuizQuestion question, int position)
        {
            GameLog.Safe(() =>
            {
                if (quiz == null || question == null) return;
                quiz.Position = position;
                quiz.QuestionId = question.id;
                quiz.QuestionStart = LogHelpers.Now;
                quiz.QuestionStartTs = LogHelpers.IsoNow();

                GameLog.Emit("quiz", "quiz_question_start", new Dictionary<string, object>
                {
                    ["quiz_type"] = Name(quiz.Type),
                    ["attempt_this_session"] = quiz.AttemptThisSession,
                    ["question_id"] = question.id,
                    ["position"] = position + 1,
                    ["start_ts"] = quiz.QuestionStartTs,
                });
            });
        }

        /// <summary>The player picked an option. picked and question.answer are indexes into question.options (A = 0).</summary>
        public static void QuestionAnswered(QuizQuestion question, int position, int picked)
        {
            GameLog.Safe(() =>
            {
                if (quiz == null || question == null) return;
                bool correct = picked == question.answer;
                quiz.Answered++;
                if (correct) quiz.Correct++;

                GameLog.Emit("quiz", "quiz_question_answer", new Dictionary<string, object>
                {
                    ["quiz_type"] = Name(quiz.Type),
                    ["attempt_this_session"] = quiz.AttemptThisSession,
                    ["question_id"] = question.id,
                    ["position"] = position + 1,
                    ["start_ts"] = quiz.QuestionStartTs,
                    ["stop_ts"] = LogHelpers.IsoNow(),
                    ["duration_seconds"] = LogHelpers.Round(LogHelpers.Now - quiz.QuestionStart),
                    ["correct_answer"] = Letter(question.answer),
                    ["correct_answer_text"] = OptionText(question, question.answer),
                    ["player_response"] = Letter(picked),
                    ["player_response_text"] = OptionText(question, picked),
                    ["is_correct"] = correct,
                });
            });
        }

        /// <summary>The last question was answered. Logs the overall result.</summary>
        public static void Finished(int moneyEarned)
        {
            GameLog.Safe(() =>
            {
                if (quiz == null) return;
                int total = quiz.Sequence.Count;
                GameLog.Emit("quiz", "quiz_end", new Dictionary<string, object>
                {
                    ["quiz_type"] = Name(quiz.Type),
                    ["attempt_this_session"] = quiz.AttemptThisSession,
                    ["attempt_all_time"] = quiz.AttemptAllTime,
                    ["question_sequence"] = quiz.Sequence,
                    ["start_ts"] = quiz.StartTs,
                    ["stop_ts"] = LogHelpers.IsoNow(),
                    ["duration_seconds"] = LogHelpers.Round(LogHelpers.Now - quiz.Start),
                    ["score_correct"] = quiz.Correct,
                    ["score_total"] = total,
                    ["score_percent"] = total > 0 ? System.Math.Round(100.0 * quiz.Correct / total, 1) : 0,
                    ["money_earned"] = moneyEarned,
                }, force: true);
                quiz = null;
            });
        }

        /// <summary>Logs quiz_interrupted if a quiz is open. Called by GameLog (session end, save loaded, failed day).</summary>
        internal static void Interrupt(string reason)
        {
            if (quiz == null) return;
            GameLog.Emit("quiz", "quiz_interrupted", new Dictionary<string, object>
            {
                ["interrupted_by"] = reason,
                ["quiz_type"] = Name(quiz.Type),
                ["attempt_this_session"] = quiz.AttemptThisSession,
                ["attempt_all_time"] = quiz.AttemptAllTime,
                ["question_sequence"] = quiz.Sequence,
                ["start_ts"] = quiz.StartTs,
                ["duration_seconds"] = LogHelpers.Round(LogHelpers.Now - quiz.Start),
                ["questions_answered"] = quiz.Answered,
                ["score_correct_so_far"] = quiz.Correct,
                ["current_question_id"] = quiz.Position >= 0 ? quiz.QuestionId : (object)null,
            }, force: true);
            quiz = null;
        }

        // ---------- helpers ----------

        private static string Name(QuizType type) => type == QuizType.MidGame ? "mid" : "post";

        private static string Letter(int index) => index >= 0 ? ((char)('A' + index)).ToString() : null;

        private static string OptionText(QuizQuestion q, int index) =>
            q.options != null && index >= 0 && index < q.options.Count ? q.options[index] : null;
    }
}
