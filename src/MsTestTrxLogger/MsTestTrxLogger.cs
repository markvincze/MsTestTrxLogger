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
        private string _trxFilePath;
        private const string LogFileNameKey = "LogFileName";

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

            parameters.TryGetValue(LogFileNameKey, out string logFileNameValue);
            string testResultsDirPath = parameters[DefaultLoggerParameterNames.TestRunDirectory];
            DeriveTrxFilePath(logFileNameValue, testResultsDirPath);
            Initialize(events, testResultsDirPath);
        }

        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            Console.WriteLine("Initializing MsTestTrxLogger.");
            Console.WriteLine("Test Run Directory: {0}", testRunDirectory);

            var testRunStarted = DateTime.Now;
            List<TestResult> testResults = new List<TestResult>();
            
            events.TestResult += (sender, eventArgs) =>
            {
                try
                {
                    testResults.Add(eventArgs.Result);                    
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

                    trxOutputWriter.WriteTrxOutput(_trxFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            };
        }

        private void DeriveTrxFilePath(string logFileNameValue, string testResultsDirPath)
        {
            if (string.IsNullOrWhiteSpace(logFileNameValue))
            {
                SetDefaultTrxFilePath(testResultsDirPath);
            }
            else
            {
                _trxFilePath = Path.Combine(testResultsDirPath, logFileNameValue);
            }
        }

        /// <summary>
        /// Sets auto generated Trx file name under test results directory.
        /// </summary>
        private void SetDefaultTrxFilePath(string testResultsDirPath)
        {
            _trxFilePath = Path.Combine(
                testResultsDirPath,
                String.Format(
                    "{0}_{1} {2}.trx",
                    Environment.UserName,
                    Environment.MachineName,
                    DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss")));
        }

    }
}