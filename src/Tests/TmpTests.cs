using System.Collections;

namespace Tests;

public class TmpTests
{
    
    [Test, TestCaseSource(typeof(TestCases), nameof(TestCases.GetTestCases))]
    public void Test(
        bool actualResult,
        bool expectedResult)
    {
        Assert.That(expectedResult, Is.EqualTo(actualResult));
    }

    private class TestCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData(true, true)
                    .SetName("Шаблонный тест");
            }
        }
    }
}