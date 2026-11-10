// RequestConfig - turns requests.csv into the RequestData list the ClientGenerator deals clients from.
//
// Columns (any order, extra columns like "Notes" are ignored):
//   ID            a whole number, 1 or more, never reused. Saves remember requests by this number.
//   Client Name   who brings the request
//   Problem       what the client says
//   Researchable  yes / no. Leave it blank and it's worked out: a request with a Best Method is researchable
//   Approach      Quantitative, Qualitative or Mixed (step one of matching)
//   Best Method   the methodology name from methodologies.csv that scores best (step two)
//   Good Methods  other methodology names that get partial credit, separated by ; or ,
//   Rationale     why the best method is the best one. Shown in the day summary
//
// Needs the methodologies already loaded, because the names are turned into methodology IDs here.

using System.Collections.Generic;
using GameDataObjects;

namespace Config
{
    public static class RequestConfig
    {
        public const string FileName = "requests.csv";

        static readonly string[] IdColumn = { "ID", "Request ID", "Number" };
        static readonly string[] NameColumn = { "Client Name", "Client", "Name" };
        static readonly string[] ProblemColumn = { "Problem", "Problem Text", "Request" };
        static readonly string[] ResearchableColumn = { "Researchable", "Can Be Researched", "Is Researchable" };
        static readonly string[] ApproachColumn = { "Approach", "Research Approach", "Method Type" };
        static readonly string[] BestColumn = { "Best Method", "Best Methodology", "Best" };
        static readonly string[] GoodColumn = { "Good Methods", "Good Methodologies", "Good" };
        static readonly string[] RationaleColumn = { "Rationale", "Why", "Explanation", "Reason" };

        /// <summary>Reads the text of requests.csv. Rows with problems are skipped or fixed, and listed in the report.</summary>
        public static List<RequestData> Parse(string csvText, IReadOnlyList<MethodEntry> methods, ConfigReport report)
        {
            List<RequestData> requests = new List<RequestData>();
            CsvTable table = CsvTable.Parse(csvText, FileName);

            if (!table.HasColumn(IdColumn) || !table.HasColumn(ProblemColumn))
            {
                report.Add(FileName, "the first row must have an \"ID\" column and a \"Problem\" column.");
                return requests;
            }
            foreach (string[] optional in new[] { NameColumn, ApproachColumn, BestColumn, GoodColumn, RationaleColumn })
            {
                if (!table.HasColumn(optional))
                    report.Add(FileName, $"there is no \"{optional[0]}\" column, so it's blank for every request.");
            }

            HashSet<int> usedIds = new HashSet<int>();

            for (int r = 0; r < table.RowCount; r++)
            {
                int row = table.SpreadsheetRow(r);
                RequestData request = ParseRow(table, r, row, methods, report);
                if (request == null) continue;

                if (!usedIds.Add(request.RequestId))
                {
                    report.Add(FileName, row, $"ID {request.RequestId} is already used by another request. Row skipped.");
                    continue;
                }
                requests.Add(request);
            }

            if (requests.Count == 0)
                report.Add(FileName, "no requests were loaded.");
            return requests;
        }

        // one row -> one request, or null if the row can't be used at all
        private static RequestData ParseRow(CsvTable table, int r, int row, IReadOnlyList<MethodEntry> methods, ConfigReport report)
        {
            string idText = table.Get(r, IdColumn);
            if (!int.TryParse(idText, out int id) || id <= 0)
            {
                report.Add(FileName, row, $"ID \"{idText}\" must be a whole number of 1 or more. Row skipped.");
                return null;
            }

            string problem = table.Get(r, ProblemColumn);
            if (problem.Length == 0)
            {
                report.Add(FileName, row, $"request {id} has no Problem text. Row skipped.");
                return null;
            }

            RequestData request = new RequestData
            {
                RequestId = id,
                Name = table.Get(r, NameColumn),
                ProblemText = problem,
                Rationale = table.Get(r, RationaleColumn)
            };
            if (request.Name.Length == 0)
                report.Add(FileName, row, $"request {id} has no Client Name (it still loads).");

            // the answers
            request.Approach = ParseApproach(table.Get(r, ApproachColumn), row, report);
            request.BestMethodologyId = LookUpMethod(table.Get(r, BestColumn), methods, row, report);
            foreach (string name in CsvTable.SplitList(table.Get(r, GoodColumn)))
            {
                int goodId = LookUpMethod(name, methods, row, report);
                if (goodId > 0 && goodId != request.BestMethodologyId && !request.GoodMethodologyIds.Contains(goodId))
                    request.GoodMethodologyIds.Add(goodId);
            }

            bool hasAnswer = request.BestMethodologyId > 0;
            request.IsResearchable = ParseResearchable(table.Get(r, ResearchableColumn), hasAnswer, row, report);

            if (request.IsResearchable)
            {
                if (!hasAnswer)
                    report.Add(FileName, row, $"request {id} is researchable but has no Best Method, so nothing can score best.");
                if (request.Approach == ResearchApproach.None)
                    report.Add(FileName, row, $"request {id} is researchable but has no Approach (Quantitative, Qualitative or Mixed).");
                if (request.Rationale.Length == 0)
                    report.Add(FileName, row, $"request {id} has no Rationale, so the day summary can't explain the answer.");
            }
            else if (hasAnswer || request.Approach != ResearchApproach.None || request.GoodMethodologyIds.Count > 0)
            {
                // "can't be researched" wins, so the answers are cleared to keep the data consistent
                report.Add(FileName, row, $"request {id} counts as not researchable (Researchable is \"no\", or blank with no Best Method), " +
                                          "so its Approach and methods are ignored.");
                request.Approach = ResearchApproach.None;
                request.BestMethodologyId = 0;
                request.GoodMethodologyIds.Clear();
            }

            return request;
        }

        private static ResearchApproach ParseApproach(string text, int row, ConfigReport report)
        {
            switch (CsvTable.Normalize(text))
            {
                case "": return ResearchApproach.None;
                case "quantitative": case "quantitativeresearch": case "quant":
                    return ResearchApproach.Quantitative;
                case "qualitative": case "qualitativeresearch": case "qual":
                    return ResearchApproach.Qualitative;
                case "mixed": case "mixedmethod": case "mixedmethods": case "mixedmethodsresearch": case "both":
                    return ResearchApproach.Mixed;
                default:
                    report.Add(FileName, row, $"Approach \"{text}\" isn't one of Quantitative, Qualitative or Mixed. Left blank.");
                    return ResearchApproach.None;
            }
        }

        private static bool ParseResearchable(string text, bool hasAnswer, int row, ConfigReport report)
        {
            switch (CsvTable.Normalize(text))
            {
                case "": return hasAnswer;   // blank: researchable if a Best Method was given
                case "yes": case "y": case "true": case "1": case "x":
                    return true;
                case "no": case "n": case "false": case "0":
                    return false;
                default:
                    report.Add(FileName, row, $"Researchable \"{text}\" should be yes or no. Treated as {(hasAnswer ? "yes" : "no")}.");
                    return hasAnswer;
            }
        }

        // methodology name (or ID) -> ID. 0 if blank or unknown
        private static int LookUpMethod(string typed, IReadOnlyList<MethodEntry> methods, int row, ConfigReport report)
        {
            if (string.IsNullOrWhiteSpace(typed)) return 0;

            MethodEntry method = MethodologyConfig.Find(methods, typed);
            if (method != null) return method.id;

            report.Add(FileName, row, $"\"{typed}\" isn't in {MethodologyConfig.FileName}. " +
                                      $"Check the spelling, or add it there. Known methods: {ListNames(methods)}.");
            return 0;
        }

        private static string ListNames(IReadOnlyList<MethodEntry> methods)
        {
            if (methods == null || methods.Count == 0) return "(none loaded)";
            List<string> names = new List<string>();
            foreach (MethodEntry m in methods) names.Add(m.name);
            return string.Join(", ", names);
        }
    }
}
