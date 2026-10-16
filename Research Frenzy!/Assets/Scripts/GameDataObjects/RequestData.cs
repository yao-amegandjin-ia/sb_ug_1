using System.Collections.Generic;
using System;
using UnityEngine;

namespace Assets.Scripts.GameDataObjects
{
    //This defines Unity's built in JSON serialization to and from JSON
    [Serializable]
    public class RequestData
    {

        public string ProblemText;

        public MethodologiesEnum BestMethodology;

        public bool IsResearchable;

        public int RequestId;

        //Usually two but could be more so using a List not an array
        public List<MethodologiesEnum> GoodMethodologies;


    }
    [Serializable]
    public class RequestList
    {

        public List<RequestData> Requests;



    }
    public enum MethodologiesEnum
    {
        None,
        Basic,
        Applied,
        Exploratory,
        Field,
        Market,
        Qualitative,
        Quantitative,
        Mixed_Methods,
        RCT,
        Policy,
        Systematic,
        Product,
        Longitudinal

    }


}


