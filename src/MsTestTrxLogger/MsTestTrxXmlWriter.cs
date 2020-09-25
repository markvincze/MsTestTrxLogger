using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MsTestTrxLogger
{
    public class MsTestTrxXmlWriter
    {
        private const string adapterTypeName = "Microsoft.VisualStudio.TestTools.TestTypes.Unit.UnitTestAdapter, Microsoft.VisualStudio.QualityTools.Tips.UnitTest.Adapter, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        private const string unitTestTypeGuid = "13CDC9D9-DDB5-4fa4-A97D-D965CCFC6D4B";

        private readonly IList<TestResult> testResults;

        private readonly TestRunCompleteEventArgs completeEventArgs;

        private readonly DateTime testRunStarted;

        private readonly Dictionary<TestResult, Guid> executionIds = new Dictionary<TestResult, Guid>();

        private readonly Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();

        public MsTestTrxXmlWriter(IList<TestResult> testResults, TestRunCompleteEventArgs completeEventArgs, DateTime testRunStarted)
        {
            this.testResults = testResults;
            this.completeEventArgs = completeEventArgs;
            this.testRunStarted = testRunStarted;
        }

        public void WriteTrxOutput(string trxFilePath)
        {
            Console.WriteLine("Starting to generate trx XML output.");

            var testRunId = Guid.NewGuid();

            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "TestRun",
                    new XAttribute("id", testRunId.ToString()),
                    new XAttribute("name", $"{Environment.UserName}@{Environment.MachineName} {DateTime.UtcNow}"),
                    new XAttribute("runUser", $@"{Environment.UserDomainName}\{Environment.UserName}"),
                    new XElement("Results",
                        testResults.Select(result => new XElement("UnitTestResult",
                            new XAttribute("computerName", Environment.MachineName),
                            new XAttribute("duration", result.Duration.ToString()),
                            new XAttribute("endTime", result.EndTime.ToString("o")),
                            new XAttribute("executionId", GetExecutionId(result)),
                            new XAttribute("outcome", result.Outcome == TestOutcome.Skipped ? "NotExecuted" : result.Outcome.ToString()),
                            new XAttribute("relativeResultsDirectory", GetExecutionId(result)),
                            new XAttribute("startTime", result.StartTime.ToString("o")),
                            new XAttribute("testId", UnitTestIdGenerator.GuidFromString(result.TestCase.FullyQualifiedName)),
                            new XAttribute("testListId", testRunId.ToString()),
                            new XAttribute("testName", result.TestCase.DisplayName),
                            new XAttribute("testType", unitTestTypeGuid),
                            GetOutputElement(result)))),
                    new XElement("ResultSummary",
                        new XAttribute("outcome", completeEventArgs.IsAborted ? "Aborted" : completeEventArgs.IsCanceled ? "Canceled" : "Completed"),
                        new XElement("Counters",
                            new XAttribute("aborted", 0),
                            new XAttribute("completed", 0),
                            new XAttribute("disconnected", 0),
                            new XAttribute("error", 0),
                            new XAttribute("executed", testResults.Count(r => r.Outcome != TestOutcome.Skipped)),
                            new XAttribute("failed", testResults.Count(r => r.Outcome == TestOutcome.Failed)),
                            new XAttribute("inconclusive", testResults.Count(r => r.Outcome == TestOutcome.Skipped || r.Outcome == TestOutcome.NotFound || r.Outcome == TestOutcome.None)),
                            new XAttribute("inProgress", 0),
                            new XAttribute("notExecuted", testResults.Count(r => r.Outcome == TestOutcome.Skipped)),
                            new XAttribute("notRunnable", 0),
                            new XAttribute("passed", testResults.Count(r => r.Outcome == TestOutcome.Passed)),
                            new XAttribute("passedButRunAborted", 0),
                            new XAttribute("pending", 0),
                            new XAttribute("timeout", 0),
                            new XAttribute("total", testResults.Count),
                            new XAttribute("warning", 0))),
                      new XElement("TestDefinitions",
                        testResults.Select(result => new XElement("UnitTest",
                            new XAttribute("id", UnitTestIdGenerator.GuidFromString(result.TestCase.FullyQualifiedName)),
                            new XAttribute("name", result.TestCase.DisplayName),
                            new XAttribute("storage", result.TestCase.Source),
                            new XElement("Description", GetDescription(result)),
                            new XElement("Execution", new XAttribute("id", GetExecutionId(result))),
                            new XElement("Properties",
                                GetPropertyAttributes(result).Select(p => new XElement("Property",
                                    new XElement("Key", p.Name),
                                    new XElement("Value", p.Value)))),
                            new XElement("TestCategory",
                                GetTestCategory(result).SelectMany( c => c.TestCategories).Select(c => new XElement("TestCategoryItem", c))),
                            new XElement("TestMethod",
                                new XAttribute("adapterTypeName", adapterTypeName),
                                new XAttribute("className", GetClassFullName(result)),
                                new XAttribute("codeBase", result.TestCase.Source),
                                new XAttribute("name", result.TestCase.DisplayName))))),
                      new XElement("TestEntries",
                        testResults.Select(result => new XElement("TestEntry",
                            new XAttribute("executionId", GetExecutionId(result)),
                            new XAttribute("testId", UnitTestIdGenerator.GuidFromString(result.TestCase.FullyQualifiedName)),
                            new XAttribute("testListId", testRunId.ToString())))),
                      new XElement("TestLists",
                        new XElement("TestList", new XAttribute("id", testRunId.ToString()), new XAttribute("name", "All Loaded Results"))),
                      new XElement("Times",
                        new XAttribute("creation", testRunStarted.ToString("o")),
                        new XAttribute("finish", DateTime.Now.ToString("o")),
                        new XAttribute("queuing", testRunStarted.ToString("o")),
                        new XAttribute("start", testRunStarted.ToString("o")))
                    ));

            // We only want to have the xmlns present on the root tag, and not on the descendants, so we're cleaning all the namespaces.
            CleanXmlNamespaces(doc);

            Console.WriteLine("XML generation done, saving the trx files.");

            File.WriteAllText(trxFilePath, doc.ToString());

            Console.WriteLine("Results File: {0}", trxFilePath);
        }

        private static void CleanXmlNamespaces(XDocument doc)
        {
            if (doc == null || doc.Root == null)
            {
                return;
            }

            foreach (var node in doc.Root.Descendants())
            {
                if (node.Name.NamespaceName == "")
                {
                    node.Attributes("xmlns").Remove();
                    node.Name = node.Parent.Name.Namespace + node.Name.LocalName;
                }
            }
        }

        /// <summary>
        /// Returns the Output tag containing both the normal test output, and the error message (if there was any).
        /// </summary>
        private XElement GetOutputElement(TestResult result)
        {
            var element = new XElement("Output",
                new XElement("StdOut",
                    String.Join(Environment.NewLine, result.Messages.Select(m => m.Text).ToArray())));

            if (!String.IsNullOrEmpty(result.ErrorMessage) || !String.IsNullOrEmpty(result.ErrorStackTrace))
            {
                element.Add(new XElement("ErrorInfo",
                    new XElement("Message", result.ErrorMessage),
                    new XElement("StackTrace", result.ErrorStackTrace)));
            }

            return element;
        }

        /// <summary>
        /// Returns the execution id for the given test.
        /// </summary>
        /// <remarks>
        /// The execution ids can be generated randomly, but we have to use the same id for a given test in multiple places in the XML.
        /// Hence the ids are stored in a dictionary for every test.
        /// </remarks>
        private Guid GetExecutionId(TestResult result)
        {
            if (!executionIds.ContainsKey(result))
            {
                executionIds.Add(result, Guid.NewGuid());
            }

            return executionIds[result];
        }

        /// <summary>
        /// Loads the assembly from <paramref name="path" /> and stores its reference so that we don't load an assembly multiple times.
        /// </summary>
        private Assembly GetAssembly(string path)
        {
            if (!assemblies.ContainsKey(path))
            {
                assemblies.Add(path, Assembly.LoadFrom(path));
            }

            return assemblies[path];
        }

        /// <summary>
        /// Returns the full description text of the unit test.
        /// </summary>
        /// <remarks>
        /// The description text (specified with <see cref="Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute" />) is not present
        /// in the object model provided in Microsoft.VisualStudio.TestPlatform.ObjectModel, so we have to look it up in the unit test assembly.
        /// </remarks>
        private string GetDescription(TestResult test)
        {
            var assembly = GetAssembly(test.TestCase.Source);

            var className = test.TestCase.FullyQualifiedName.Substring(0, test.TestCase.FullyQualifiedName.LastIndexOf('.'));
            var methodName = test.TestCase.FullyQualifiedName.Substring(test.TestCase.FullyQualifiedName.LastIndexOf('.') + 1);

            var type = assembly.GetType(className);

            var method = type.GetMethod(methodName);

            var attributes = method.GetCustomAttributes<DescriptionAttribute>().ToList();

            if (attributes.Any())
            {
                return attributes.First().Description;
            }

            return test.TestCase.DisplayName;
        }

        /// <summary>
        /// Returns the list of TestPropertyAttributes specified for <paramref name="test" />.
        /// </summary>
        /// <remarks>
        /// The information in the TestPropertyAttributes (<see cref="Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute" />) is not present
        /// in the object model provided in Microsoft.VisualStudio.TestPlatform.ObjectModel, so we have to look it up in the unit test assembly.
        /// </remarks>
        private IEnumerable<TestPropertyAttribute> GetPropertyAttributes(TestResult test)
        {
            var assembly = GetAssembly(test.TestCase.Source);

            var className = test.TestCase.FullyQualifiedName.Substring(0, test.TestCase.FullyQualifiedName.LastIndexOf('.'));
            var methodName = test.TestCase.FullyQualifiedName.Substring(test.TestCase.FullyQualifiedName.LastIndexOf('.') + 1);

            var type = assembly.GetType(className);

            var method = type.GetMethod(methodName);

            return method.GetCustomAttributes<TestPropertyAttribute>();
        }

        /// <summary>
        /// Returns the list of TestCategoryAttributes specified for <paramref name="test" />.
        /// </summary>
        /// <remarks>
        /// The information in the TestCategoryAttributes (<see cref="Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute" />) is not present
        /// in the object model provided in Microsoft.VisualStudio.TestPlatform.ObjectModel, so we have to look it up in the unit test assembly.
        /// </remarks>
        private IEnumerable<TestCategoryBaseAttribute> GetTestCategory(TestResult test)
        {
            var assembly = GetAssembly(test.TestCase.Source);

            var className = test.TestCase.FullyQualifiedName.Substring(0, test.TestCase.FullyQualifiedName.LastIndexOf('.'));
            var methodName = test.TestCase.FullyQualifiedName.Substring(test.TestCase.FullyQualifiedName.LastIndexOf('.') + 1);

            var type = assembly.GetType(className);

            var method = type.GetMethod(methodName);

            return method.GetCustomAttributes<TestCategoryBaseAttribute>();
        }

        /// <summary>
        /// Returns the fully qualified assembly name of the unit test class.
        /// </summary>
        /// <remarks>
        /// This information  is not present in the object model provided in Microsoft.VisualStudio.TestPlatform.ObjectModel, so we have to look it up in the unit test assembly.
        /// </remarks>
        private string GetClassFullName(TestResult test)
        {
            var assembly = GetAssembly(test.TestCase.Source);

            var className = test.TestCase.FullyQualifiedName.Substring(0, test.TestCase.FullyQualifiedName.LastIndexOf('.'));

            var type = assembly.GetType(className);

            return type.AssemblyQualifiedName;
        }
    }
}
