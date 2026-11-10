using System;
using System.Collections.Generic;

namespace GameDataObjects
{
    /// <summary>
    /// Step one of a match: the broad kind of research.
    /// None means "no answer", which is what requests that can't be researched have.
    /// </summary>
    public enum ResearchApproach
    {
        None,
        Quantitative,
        Qualitative,
        Mixed
    }

    /// <summary>
    /// One client request, as read from requests.csv by RequestConfig.
    /// Methodologies are stored by their ID from methodologies.csv (not an enum),
    /// so new methodologies can be added in the file without changing code.
    /// </summary>
    [Serializable]
    public class RequestData
    {
        public int RequestId;

        // name of the client who brings the request
        public string Name;

        public string ProblemText;

        // false = the right answer is "We can't help"
        public bool IsResearchable;

        // step one answer: quantitative, qualitative or mixed
        public ResearchApproach Approach;

        // step two answer: the methodology ID that scores best. 0 = none
        public int BestMethodologyId;

        // methodology IDs that get partial credit. Usually two but could be more, so a List
        public List<int> GoodMethodologyIds = new List<int>();

        // why the best method is best. shown to the player in DaySummary
        public string Rationale;
    }

    /// <summary>Wrapper so JsonUtility (and the Inspector) can handle the list.</summary>
    [Serializable]
    public class RequestList
    {
        public List<RequestData> Requests = new List<RequestData>();
    }
}