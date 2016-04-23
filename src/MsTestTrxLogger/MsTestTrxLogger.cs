using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace MsTestTrxLogger
{
    [ExtensionUri("logger://MsTestTrxLogger/v1")]
    [FriendlyName("MsTestTrxLogger")]
    public class MsTestTrxLogger : ITestLogger
    {
        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            Console.WriteLine("Initializing MsTestTrxLogger.");
            Console.WriteLine("testRunDirectory {0}", testRunDirectory);

            var testRunStarted = DateTime.Now;
            List<TestResult> testResults = new List<TestResult>();

            events.TestResult += (sender, eventArgs) =>
            {
                try
                {
                    if (!IsTestIgnored(eventArgs.Result))
                    {
                        testResults.Add(eventArgs.Result);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            };

            events.TestRunMessage += (sender, args) =>
            {
                if (args != null)
                {
                    Console.WriteLine(args.Message);
                }
            };

            events.TestRunComplete += (sender, args) =>
            {
                try
                {
                    var trxOutputWriter = new MsTestTrxXmlWriter(testResults, args, testRunStarted);

                    trxOutputWriter.WriteTrxOutput(testRunDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            };
        }

        /// <summary>
        /// Returns whether the test was ignored or not.
        /// </summary>
        /// <remarks>
        /// The object model doesn't indicate whether a test was ignored with an IgnoreAttribute, or was skipped for other reasons.
        /// It seems to be a reliable way to recognize if a test was actually ignored if we check whether the number of its Messages id 0.
        /// </remarks>
        private bool IsTestIgnored(TestResult test) => test.Outcome == TestOutcome.Skipped && test.Messages.Count == 0;
    }
}