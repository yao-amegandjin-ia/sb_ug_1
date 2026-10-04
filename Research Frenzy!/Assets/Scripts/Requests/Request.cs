using System.Collections.Generic;

using UnityEngine;

public class Request
{

    public string ProblemText { get; }

    //Probably best to create an enum for these instead of ids 
    public MethodologiesEnum BestMethodology { get; }

    public bool IsResearchable { get; }

    public int RequestId{ get; }

    //This is an immutable list so okay to have a getter
    public List<MethodologiesEnum> GoodMethodologyIds { get; }
    
}
