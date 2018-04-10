using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace MsTestTrxLogger
{
    [ExtensionUri("logger://MsTestTrxLogger/v1")]
    [FriendlyName("MsTestTrxLogger")]
    public class MsTestTrxLogger : ITestLoggerWithParameters
    {
        /// <summary>
        /// Cache the TRX file path
        /// </summary>
        private string trxFilePath;

        /// <summary>
        /// Parameters dictionary for logger. Ex: {"LogFileName":"TestResults.trx"}.
        /// </summary>
        private Dictionary<string, string> parametersDictionary;
        private string LogFileNameKey = "LogFileName";
        private string testResultsDirPath;

        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            Console.WriteLine("Initializing MsTestTrxLogger.");
            Console.WriteLine("Test Run Directory: {0}", testRunDirectory);

            var testRunStarted = DateTime.Now;
            List<TestResult> testResults = new List<TestResult>();
            testResultsDirPath = testRunDirectory;
            DeriveTrxFilePath();

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

                    trxOutputWriter.WriteTrxOutput(testRunDirectory, trxFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            };
        }

        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.Count == 0)
            {
                throw new ArgumentException("No default parameters added", nameof(parameters));
            }

            parametersDictionary = parameters;
            Initialize(events, parametersDictionary[DefaultLoggerParameterNames.TestRunDirectory]);
        }

        private void DeriveTrxFilePath()
        {
            if (parametersDictionary != null)
            {
                var isLogFileNameParameterExists = parametersDictionary.TryGetValue(LogFileNameKey, out string logFileNameValue);
                if (isLogFileNameParameterExists && !string.IsNullOrWhiteSpace(logFileNameValue))
                {
                    trxFilePath = Path.Combine(testResultsDirPath, logFileNameValue);
                }
                else
                {
                    SetDefaultTrxFilePath();
                }
            }
            else
            {
                SetDefaultTrxFilePath();
            }
        }

        /// <summary>
        /// Sets auto generated Trx file name under test results directory.
        /// </summary>
        private void SetDefaultTrxFilePath()
        {
            trxFilePath = Path.Combine(
                testResultsDirPath,
                String.Format(
                    "{0}_{1} {2}.trx",
                    Environment.UserName,
                    Environment.MachineName,
                    DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss")));
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