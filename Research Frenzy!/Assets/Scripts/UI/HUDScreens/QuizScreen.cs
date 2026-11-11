// QuizScreen - goes on the GameUI object. mid game and post game quiz, questions from Resources/Config/ResearchFrenzyQuizzes.csv
// same questions every time, order is shuffled with the game seed. results only show at the end.
// TODO: $50 per right answer + sending answers with SaveQuizData 

using System;
using System.Collections.Generic;
using Config;
using Interfaces;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.HUDScreens
{
    public class QuizQuestion
    {
        public int id;
        public QuizType quiz;
        public string question;
        public List<string> options = new List<string>();
        public int answer;   // index into options, starts at 0
    }

    // one question the player answered
    public class QuizAnswer
    {
        public QuizQuestion question;
        public int picked;
        public float seconds;

        public bool IsCorrect()
        {
            return picked == question.answer;
        }
    }

    public class QuizScreen : MonoBehaviour, IGameUIPart
    {
        public const string FileName = "ResearchFrenzyQuizzes.csv";

        public int correctReward = 50;
        public bool showDemoButtons = true;

        // fires when the player presses Continue on the results (which quiz, every answer in the order they saw them).
        // the money and the backend save can hook in here
        public event Action<QuizType, List<QuizAnswer>> Finished;

        // the results of the last quiz, in the order the player saw them
        public List<QuizAnswer> answers = new List<QuizAnswer>();

        const int QuizStreamId = 3000;   // for GameSeed, so shuffling the quiz doesn't change the clients or researchers

        List<QuizQuestion> allQuestions;   // both quizzes, loaded the first time a quiz opens
        List<QuizQuestion> questions = new List<QuizQuestion>();
        QuizType type;
        int current = -1;        // the question on screen, -1 when no question is showing
        bool showingResults;
        float questionStart;     // when the current question showed up

        VisualElement panel;
        VisualElement modal;
        VisualElement questionPage;
        VisualElement resultsPage;
        VisualElement options;
        VisualElement tiles;
        ScrollView list;
        Label title;
        Label progress;
        Label questionText;
        Label earnedText;
        Label totalText;
        HUD hud;

        void Awake()
        {
            hud = GetComponent<HUD>();
        }

        // called by GameUIPanel once the UI has loaded (and again if it reloads)
        public void Bind(VisualElement root)
        {
            panel = root.Q("quiz-screen");
            if (panel == null)
            {
                Debug.LogWarning("QuizScreen: no element named 'quiz-screen' in the GameUI UXML.");
                return;
            }
            modal = root.Q("quiz-modal");
            questionPage = root.Q("quiz-question-page");
            resultsPage = root.Q("quiz-results-page");
            options = root.Q("quiz-options");
            tiles = root.Q("quiz-tiles");
            list = root.Q<ScrollView>("quiz-list");
            title = root.Q<Label>("quiz-title");
            progress = root.Q<Label>("quiz-progress");
            questionText = root.Q<Label>("quiz-question");
            earnedText = root.Q<Label>("quiz-earned");
            totalText = root.Q<Label>("quiz-total");

            Button continueButton = root.Q<Button>("quiz-continue");
            if (continueButton != null) continueButton.clicked += ContinueClicked;
            else Debug.LogWarning("QuizScreen: no Button named 'quiz-continue' in the GameUI UXML.");

            if (showDemoButtons)
            {
                AddDemoButton(root, "Mid Quiz", 88, MidQuizClicked);
                AddDemoButton(root, "Post Quiz", 124, PostQuizClicked);
            }

            // the UI reloaded while a quiz was open
            if (showingResults) ShowResults();
            else if (current >= 0) ShowQuestion();
        }

        // same look and spot as the Spawn Client / Upgrades demo buttons, just under them
        void AddDemoButton(VisualElement root, string text, int top, Action onClick)
        {
            Button button = new Button(onClick);
            button.text = text;
            button.style.position = Position.Absolute;
            button.style.top = top;
            button.style.right = 16;
            root.Add(button);
        }

        void MidQuizClicked()
        {
            Open(QuizType.MidGame);
        }

        void PostQuizClicked()
        {
            Open(QuizType.PostGame);
        }

        public void Open(QuizType quizType)
        {
            if (panel == null)
            {
                Debug.LogWarning("QuizScreen: the UI hasn't loaded yet.");
                return;
            }
            if (allQuestions == null) allQuestions = LoadQuestions();

            questions = new List<QuizQuestion>();
            foreach (QuizQuestion q in allQuestions)
                if (q.quiz == quizType) questions.Add(q);

            if (questions.Count == 0)
            {
                Debug.LogWarning("QuizScreen: no " + quizType + " questions in " + FileName);
                return;
            }

            type = quizType;
            Shuffle(questions, GameSeed.CreateRandom(QuizStreamId, (int)type));
            answers.Clear();
            current = 0;
            showingResults = false;

            // both of these use the left side and would sit behind the quiz
            CodexPanel codex = GetComponent<CodexPanel>();
            if (codex != null) codex.Close();
            ManageFirmPanel firm = GetComponent<ManageFirmPanel>();
            if (firm != null) firm.Close();

            title.text = type == QuizType.MidGame ? "QUICK CHECK-IN" : "FINAL QUIZ";
            questionStart = Time.unscaledTime;
            ShowQuestion();
            panel.RemoveFromClassList("hidden");
        }

        // reads the spreadsheet. columns: ID, Quiz (Mid or Post), Question, A, B, C, D, Answer (the letter)
        List<QuizQuestion> LoadQuestions()
        {
            List<QuizQuestion> loaded = new List<QuizQuestion>();
            ConfigReport report = new ConfigReport();

            string text = ConfigFiles.Read(FileName, report);
            if (text != null)
            {
                CsvTable table = CsvTable.Parse(text, FileName);
                for (int row = 0; row < table.RowCount; row++)
                {
                    QuizQuestion q = ReadRow(table, row, report);
                    if (q != null) loaded.Add(q);
                }
            }

            ConfigFiles.Log(report);
            return loaded;
        }

        // one row of the spreadsheet, or null (with a report entry) if something in it is wrong
        QuizQuestion ReadRow(CsvTable table, int row, ConfigReport report)
        {
            int line = table.SpreadsheetRow(row);
            QuizQuestion q = new QuizQuestion();

            if (!int.TryParse(table.Get(row, "ID"), out q.id))
            {
                report.Add(FileName, line, "the ID isn't a number");
                return null;
            }

            string quiz = table.Get(row, "Quiz").ToLower();
            if (quiz.StartsWith("mid")) q.quiz = QuizType.MidGame;
            else if (quiz.StartsWith("post")) q.quiz = QuizType.PostGame;
            else
            {
                report.Add(FileName, line, "Quiz should be Mid or Post");
                return null;
            }

            q.question = table.Get(row, "Question");
            if (q.question == "")
            {
                report.Add(FileName, line, "the question is empty");
                return null;
            }

            // empty option cells are skipped, so a question can have 2 or 3 options too
            string[] letters = { "A", "B", "C", "D" };
            int answerIndex = -1;
            string answer = table.Get(row, "Answer").ToUpper();
            foreach (string letter in letters)
            {
                string option = table.Get(row, letter);
                if (option == "") continue;
                if (letter == answer) answerIndex = q.options.Count;
                q.options.Add(option);
            }

            if (q.options.Count < 2)
            {
                report.Add(FileName, line, "needs at least 2 options");
                return null;
            }
            if (answerIndex < 0)
            {
                report.Add(FileName, line, "Answer should be the letter of one of the options (A, B, C or D)");
                return null;
            }
            q.answer = answerIndex;
            return q;
        }

        // Fisher-Yates
        void Shuffle(List<QuizQuestion> items, System.Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                QuizQuestion temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }

        // --- answering ---

        void ShowQuestion()
        {
            QuizQuestion q = questions[current];

            modal.RemoveFromClassList("summary-modal");
            questionPage.RemoveFromClassList("hidden");
            resultsPage.AddToClassList("hidden");

            progress.text = (current + 1) + " / " + questions.Count;
            questionText.text = q.question;

            options.Clear();
            for (int i = 0; i < q.options.Count; i++)
            {
                int index = i;   // copy it, otherwise every button would use the last i
                Button button = new Button(() => OptionClicked(index));
                button.text = Letter(i) + ".   " + q.options[i];
                button.AddToClassList("quiz-option");
                options.Add(button);
            }
        }

        void OptionClicked(int picked)
        {
            if (current < 0 || showingResults) return;

            QuizAnswer answer = new QuizAnswer();
            answer.question = questions[current];
            answer.picked = picked;
            answer.seconds = Time.unscaledTime - questionStart;
            answers.Add(answer);

            current++;
            questionStart = Time.unscaledTime;
            if (current < questions.Count) ShowQuestion();
            else FinishQuiz();
        }

        void FinishQuiz()
        {
            current = -1;
            showingResults = true;

            Debug.Log(type + " quiz done: " + CorrectCount() + "/" + answers.Count + " right in " + FormatTime(TotalTime()));
            foreach (QuizAnswer a in answers)
                Debug.Log("  question " + a.question.id + ": picked " + Letter(a.picked) + (a.IsCorrect() ? " (right)" : " (wrong)") + ", " + a.seconds.ToString("0.0") + "s");

            // TODO: give CorrectCount() * correctReward money and send {question id: picked} with SaveQuizData

            ShowResults();
        }

        // --- results ---

        void ShowResults()
        {
            modal.AddToClassList("summary-modal");
            questionPage.AddToClassList("hidden");
            resultsPage.RemoveFromClassList("hidden");

            int right = CorrectCount();
            progress.text = right + " / " + answers.Count + " CORRECT";

            tiles.Clear();
            AddTile("CORRECT", right.ToString(), "best");
            AddTile("INCORRECT", (answers.Count - right).ToString(), "wrong");
            AddTile("TOTAL TIME", FormatTime(TotalTime()), "good");

            list.Clear();
            for (int i = 0; i < answers.Count; i++)
                list.Add(BuildCard(i + 1, answers[i]));

            earnedText.text = "$" + (right * correctReward).ToString("N0");
            totalText.text = "$" + (hud != null ? hud.money : 0).ToString("N0");

            panel.RemoveFromClassList("hidden");
        }

        void AddTile(string label, string value, string colorClass)
        {
            VisualElement tile = new VisualElement();
            tile.AddToClassList("summary-tile");
            tile.AddToClassList(colorClass);
            Label v = new Label(value);
            v.AddToClassList("tile-value");
            Label l = new Label(label);
            l.AddToClassList("tile-label");
            tile.Add(v);
            tile.Add(l);
            tiles.Add(tile);
        }

        // one question: number, time, right/wrong, the question, what they picked, and the right answer if they missed it
        VisualElement BuildCard(int number, QuizAnswer answer)
        {
            QuizQuestion q = answer.question;

            VisualElement card = new VisualElement();
            card.AddToClassList("study-card");

            VisualElement top = new VisualElement();
            top.AddToClassList("study-card-top");
            Label name = new Label("QUESTION " + number);
            name.AddToClassList("study-client");
            top.Add(name);

            VisualElement right = new VisualElement();
            right.AddToClassList("quiz-card-right");
            Label time = new Label(answer.seconds.ToString("0.0") + "s");
            time.AddToClassList("quiz-time");
            Label tag = new Label(answer.IsCorrect() ? "CORRECT" : "INCORRECT");
            tag.AddToClassList("result-tag");
            tag.AddToClassList(answer.IsCorrect() ? "best" : "wrong");
            right.Add(time);
            right.Add(tag);
            top.Add(right);
            card.Add(top);

            Label question = new Label(q.question);
            question.AddToClassList("study-problem");
            card.Add(question);

            card.Add(AnswerRow("YOUR ANSWER", OptionText(q, answer.picked)));
            if (!answer.IsCorrect())
                card.Add(AnswerRow("RIGHT ANSWER", OptionText(q, q.answer)));

            return card;
        }

        VisualElement AnswerRow(string label, string text)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("answer-row");
            Label l = new Label(label);
            l.AddToClassList("answer-label");
            Label t = new Label(text);
            t.AddToClassList("answer-text");
            row.Add(l);
            row.Add(t);
            return row;
        }

        string OptionText(QuizQuestion q, int index)
        {
            if (index < 0 || index >= q.options.Count) return "?";
            return Letter(index) + ".  " + q.options[index];
        }

        void ContinueClicked()
        {
            if (panel != null) panel.AddToClassList("hidden");
            showingResults = false;
            current = -1;
            Finished?.Invoke(type, answers);
        }

        // --- helpers ---

        public int CorrectCount()
        {
            int n = 0;
            foreach (QuizAnswer a in answers)
                if (a.IsCorrect()) n++;
            return n;
        }

        public float TotalTime()
        {
            float total = 0;
            foreach (QuizAnswer a in answers)
                total += a.seconds;
            return total;
        }

        string Letter(int index)
        {
            return ((char)('A' + index)).ToString();
        }

        string FormatTime(float seconds)
        {
            int s = Mathf.RoundToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
