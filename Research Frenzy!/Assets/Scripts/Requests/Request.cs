using System.Collections.Generic;
using System;
using UnityEngine;

namespace Assets.Scripts.Requests
{
    //This defines Unity's built in JSON serialization to and from JSON
    [Serializable]
    public class Request
    {

        public string ProblemText;

        public MethodologiesEnum BestMethodology;

        public bool IsResearchable;

        public int RequestId;

        //Usually two but could be more so using a List not an array
        public List<MethodologiesEnum> GoodMethodologies;


    }

}


