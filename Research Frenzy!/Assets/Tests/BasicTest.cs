using NUnit.Framework;
using UnityEngine;

public class BasicTests
{
    [Test]
    public void TestRuns()
    {
        Debug.Log("BASIC TEST EXECUTED");
        Assert.Pass();
    }
}