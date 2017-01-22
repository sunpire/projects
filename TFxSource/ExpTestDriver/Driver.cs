using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Expedia.Test.Framework.Log;

namespace Expedia.Test.Framework
{
    public class TestResult
    {
        public TestResult()
        {
            Verifications = new TestVerification[0];
            Counters = new TestCounter[0];
            Scenarios = new TestRunResult[0];
        }
        public TestVerification[] Verifications { get; set; }
        public TestCounter[] Counters { get; set; }
        public TestRunResult[] Scenarios { get; set; }
        public string FailureReason { get; set; }
        public string AssignmentRunResult { get; set; }
        public string ErrorCategory { get; set; }
        public string ErrorDetail { get; set; }
    }

    [Serializable]
    public class Driver : MarshalByRefObject
    {
        #region Driver Public Functions

        /// <summary>
        /// This function is called by the DriverDomainSafe class because we cannot send 
        /// parameters when we create a new domain and intialize driver
        /// </summary>
        internal static void SafeExecute()
        {
            string testFullName = (string)AppDomain.CurrentDomain.GetData("testFullName");
            string moduleFullName = (string)AppDomain.CurrentDomain.GetData("moduleFullName");
            Dictionary<string, string> config = (Dictionary<string, string>)AppDomain.CurrentDomain.GetData("configs");
            Dictionary<string, string> testParameters = (Dictionary<string, string>)AppDomain.CurrentDomain.GetData("testParameters");

            string[,] scenarios = (string[,])AppDomain.CurrentDomain.GetData("scenarios");
            string failureReason = (string)AppDomain.CurrentDomain.GetData("failureReason");
            string assignmentRunResult = (string)AppDomain.CurrentDomain.GetData("assignmentRunResult");
            string errorCategory = (string)AppDomain.CurrentDomain.GetData("errorCategory");
            string errorDetail = (string)AppDomain.CurrentDomain.GetData("errorDetail");

            Driver.Execute(testFullName, moduleFullName, config, testParameters, out scenarios, out failureReason, out assignmentRunResult, out errorCategory, out errorDetail);

            //set updated values to app domain to return to calling function
            AppDomain.CurrentDomain.SetData("scenarios", scenarios);
            AppDomain.CurrentDomain.SetData("failureReason", failureReason);
            AppDomain.CurrentDomain.SetData("assignmentRunResult", assignmentRunResult);
            AppDomain.CurrentDomain.SetData("errorCategory", errorCategory);
            AppDomain.CurrentDomain.SetData("errorDetail", errorDetail);
        }

        /// <summary>
        /// This function is called by the DriverDomainSafe class because we cannot send 
        /// parameters when we create a new domain and intialize driver
        /// </summary>
        internal static void SafeList()
        {
            string path = (string)AppDomain.CurrentDomain.GetData("path");
            TestArea testArea = Driver.List(path);
            AppDomain.CurrentDomain.SetData("testArea", testArea);
        }

        /// <summary>
        /// This function is used to get the flat list of test cases names
        /// </summary>
        public static object[] FlatList(string path)
        {
            return TestCasesList(path);
        }


        public static TestArea List(string path)
        {
            TestArea area = null;

            try
            {
                area = LoadModule(path).GetAllTestInfo();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return null;
            }

            return area;
        }

        #endregion

        #region Execute Functions

        public static void Execute(string testFullName, string moduleFullName, Dictionary<string, string> assignmentConfigs, Dictionary<string, string> testParameters, out string[,] scenarios, out string failureReason, out string assignmentRunResult, out string errorCategory, out string errorDetail)
        {
            // initialize the out parameters
            failureReason = String.Empty;
            scenarios = new string[0, 0];
            errorCategory = TestErrorCategory.Unresolved.ToString();
            errorDetail = string.Empty;

            // initialize the logger.
            IList<IDirectLog> directLogs = null;
            try
            {
                // initialize logger
                directLogs = InitializeLogger(assignmentConfigs);

                // report the test name and module name.
                Logger.Instance.Write(LogLevelType.TFx, "Run by {0} on {1}", Environment.UserName, Environment.MachineName);
                Logger.Instance.Write(LogLevelType.TFx, "Test Full Name {0}", testFullName);
                Logger.Instance.Write(LogLevelType.TFx, "Module Full Name {0}", moduleFullName);
                Logger.Instance.Write(LogLevelType.TFx, ".NET CLR Version {0}", Environment.Version);
            }
            catch (Exception e)
            {
                // Eat up any exceptions thrown by initializing the log.
                // TODO: Write log to the right place.
                Console.WriteLine("Failed to initialize logger: {0}", e);
            }

            // start actual execution.
            try
            {
                // find the method info.
                MethodInfo methodInfo = FindMethodByTestName(testFullName, moduleFullName);
                // initialize the test context.
                TestContext.DriverOnly.Initialize(methodInfo, assignmentConfigs);

                //intialize the test parameters
                if (testParameters == null)
                {
                    TestContext.DriverOnly.TestParameters = new Dictionary<string, string>();
                }
                else
                {
                    TestContext.DriverOnly.TestParameters = testParameters;
                }

                // create text fixture and set in test context.
                object test = Activator.CreateInstance(methodInfo.DeclaringType, true);
                TestContext.DriverOnly.SetTestFixture(test);

                // run the test.
                RunTest(test, methodInfo);

                // prepare the scenarios.
                TestContext.DriverOnly.PrepareScenarios();

                // Default to pass, unless there is a failed scenario below
                assignmentRunResult = TestOutcome.Pass.ToString();

                // Convert scenarios array
                // Save local copy..property does conversion to array
                TestRunResult[] testRunScenarios = TestContext.Instance.Scenarios;
                scenarios = new string[testRunScenarios.Length, 4];

                // Persist all scenarios.
                for (int i = 0; i < testRunScenarios.Length; i++)
                {
                    TestRunResult scenario = testRunScenarios[i];

                    if (scenario.IsFailure)
                    {
                        assignmentRunResult = AssignmentRunResultType.ExecutionError.ToString();

                        if (String.IsNullOrEmpty(failureReason))
                        {
                            failureReason = scenario.FailureReason;
                        }

                        errorCategory = scenario.ErrorCategory.ToString();
                        errorDetail = scenario.ErrorDetail;
                    }

                    scenarios[i, 0] = scenario.Description;         // Name
                    scenarios[i, 1] = scenario.Outcome.ToString();  // Pass/Fail
                    scenarios[i, 2] = scenario.FailureReason;       // Error message
                    scenarios[i, 3] = scenario.FailureDetail;       // Exception/StackTrace
                }
            }
            catch (TestNotFoundException e)
            {
                assignmentRunResult = AssignmentRunResultType.FailToFindTest.ToString();
                failureReason = e.ToString();
                Logger.Instance.WriteError("Test not found exception in driver execute : {0}", e.ToString());
            }
            catch (ModuleNotFoundException e)
            {
                assignmentRunResult = AssignmentRunResultType.ModuleNotFound.ToString();
                failureReason = e.ToString();
                Logger.Instance.WriteError("Module not found exception in driver execute : {0}", e.ToString());
            }
            catch (Exception e)
            {
                assignmentRunResult = AssignmentRunResultType.ExecutionError.ToString();
                failureReason = UnwrapException(e).ToString();
                Logger.Instance.WriteError("Exception in driver execute : {0}", e.ToString());
            }
            finally
            {
                // Cleanup the context.
                TestContext.DriverOnly.Cleanup();
            }

            // attempt to cleanup the logger.
            try
            {
                // cleanup the loggers.
                if (directLogs != null && directLogs.Count > 0)
                {
                    foreach (var directLog in directLogs)
                    {
                        CleanupLogger(directLog);
                    }
                }
            }
            catch (Exception e)
            {
                // Eat up any exceptions thrown by cleaning up the log.
                // TODO: Write log to the right place.
                Console.WriteLine("Failed to cleanup logger: {0}", e);
            }
        }


        public TestResult Execute(
            string testFullName,
            string moduleFullName,
            Dictionary<string, string> assignmentConfigs,
            Dictionary<string, string> testParameters)
        {
            TestResult result = new TestResult();
            result.ErrorCategory = TestErrorCategory.Unresolved.ToString();

            // initialize the logger.
            IList<IDirectLog> directLogs = null;
            try
            {
                // initialize logger
                directLogs = InitializeLogger(assignmentConfigs);

                // report the test name and module name.
                Logger.Instance.Write(LogLevelType.TFx, "Run by {0} on {1}", Environment.UserName, Environment.MachineName);
                Logger.Instance.Write(LogLevelType.TFx, "Test Full Name {0}", testFullName);
                Logger.Instance.Write(LogLevelType.TFx, "Module Full Name {0}", moduleFullName);
                Logger.Instance.Write(LogLevelType.TFx, ".NET CLR Version {0}", Environment.Version);
            }
            catch (Exception e)
            {
                // Eat up any exceptions thrown by initializing the log.
                // TODO: Write log to the right place.
                Console.WriteLine("Failed to initialize logger: {0}", e);
            }

            // start actual execution.
            try
            {
                // find the method info.
                MethodInfo methodInfo = FindMethodByTestName(testFullName, moduleFullName);
                // initialize the test context.
                TestContext.DriverOnly.Initialize(methodInfo, assignmentConfigs);

                //intialize the test parameters
                if (testParameters == null)
                {
                    TestContext.DriverOnly.TestParameters = new Dictionary<string, string>();
                }
                else
                {
                    TestContext.DriverOnly.TestParameters = testParameters;
                }

                // create text fixture and set in test context.
                object test = Activator.CreateInstance(methodInfo.DeclaringType, true);
                TestContext.DriverOnly.SetTestFixture(test);

                // run the test.
                RunTest(test, methodInfo);

                result.Verifications = TestContext.Instance.Verifications.ToArray();
                result.Counters = TestContext.Instance.Counters.ToArray();

                // prepare the scenarios.
                TestContext.DriverOnly.PrepareScenarios();

                // Convert scenarios array
                // Save local copy..property does conversion to array
                result.Scenarios = TestContext.Instance.Scenarios;

                var failureScenario = result.Scenarios.FirstOrDefault(z => z.IsFailure);

                // Default to pass, unless there is a failed scenario below
                result.AssignmentRunResult = TestOutcome.Pass.ToString();


                if (failureScenario != null)
                {
                    result.AssignmentRunResult = AssignmentRunResultType.ExecutionError.ToString();
                    result.FailureReason = failureScenario.FailureReason;
                    result.ErrorCategory = failureScenario.ErrorCategory.ToValue();
                    result.ErrorDetail = failureScenario.ErrorDetail;
                }

            }
            catch (TestNotFoundException e)
            {
                result.AssignmentRunResult = AssignmentRunResultType.FailToFindTest.ToString();
                result.FailureReason = e.ToString();
                Logger.Instance.WriteError("Test not found exception in driver execute : {0}", e.ToString());
            }
            catch (ModuleNotFoundException e)
            {
                result.AssignmentRunResult = AssignmentRunResultType.ModuleNotFound.ToString();
                result.FailureReason = e.ToString();
                Logger.Instance.WriteError("Module not found exception in driver execute : {0}", e.ToString());
            }
            catch (Exception e)
            {
                result.AssignmentRunResult = AssignmentRunResultType.ExecutionError.ToString();
                result.FailureReason = UnwrapException(e).ToString();
                Logger.Instance.WriteError("Exception in driver execute : {0}", e.ToString());
            }
            finally
            {
                // Cleanup the context.
                TestContext.DriverOnly.Cleanup();
            }

            // attempt to cleanup the logger.
            try
            {
                // cleanup the loggers.
                if (directLogs != null && directLogs.Count > 0)
                {
                    foreach (var directLog in directLogs)
                    {
                        CleanupLogger(directLog);
                    }
                }
            }
            catch (Exception e)
            {
                // Eat up any exceptions thrown by cleaning up the log.
                // TODO: Write log to the right place.
                Console.WriteLine("Failed to cleanup logger: {0}", e);
            }
            return result;
        }

        #region Run method Helpers
        /// <summary>
        /// Helper function to initialize the logger without needing TestContext to be
        /// initialized.  This function auto-attaches the direct log to the logger.
        /// </summary>
        /// <param name="configs">The configs to use to load the different types of logs.</param>
        /// <returns>An instance of the direct log.</returns>
        private static IList<IDirectLog> InitializeLogger(Dictionary<string, string> configs)
        {
            // make sure logger is not already initialized.
            if (Logger.IsInitialized)
            {
                throw new InvalidOperationException("Logger has already been initialized.");
            }

            // initialize useful variables
            IList<IDirectLog> directLogs = new List<IDirectLog>();
            string logType = configs.ContainsKey("__logtype") ? configs["__logtype"] : "FileLog";
            string logInfo = configs.ContainsKey("__loginfo") ? configs["__LogInfo"] : "Log";
            string labrunName = configs.ContainsKey("__labrunname") ? configs["__labrunname"] : "DefaultLabRun";
            string assignmentId = configs.ContainsKey("__assignmentid") ? configs["__assignmentid"] : "0";

            string logSystem = configs.ContainsKey("__LogSystem") ? configs["__LogSystem"] : string.Empty;

            LogLevelType logLevel = (LogLevelType)Enum.Parse(typeof(LogLevelType),
                configs.ContainsKey("__loglevel") ? configs["__loglevel"] : "Default", true);

            if (logSystem.Contains("DBLog"))
            {
                // try to parse the assignment id (defaults to 0)
                int numericAssignmentId;
                int.TryParse(assignmentId, out numericAssignmentId);

                // create the database log
                DirectDBLog databaseLog = new DirectDBLog();
                databaseLog.AssignmentId = numericAssignmentId;
                databaseLog.LabrunId = configs.ContainsKey("__labrunid") ? int.Parse(configs["__labrunid"]) : -1;
                databaseLog.ConnectString = logInfo;
                IDirectLog directLog = new AsyncLog(databaseLog);
                directLog.Start();
                directLogs.Add(directLog);
                Logger.Instance.AttachLogListener(logLevel, directLog);
            }

            if (logSystem.Contains("LogJam"))
            {
                string serviceUrl = configs.ContainsKey("__LogServiceUrl") ? configs["__LogServiceUrl"] : string.Empty;
                string logId = configs.ContainsKey("__LogId") ? configs["__LogId"] : Guid.NewGuid().ToString();
                string logttl = configs.ContainsKey("__LogTTL") ? configs["__LogTTL"] : string.Empty;

                var cassandraLog = new CassandraLog(serviceUrl, logId, logttl);
                IDirectLog directLog = new AsyncLog(cassandraLog);
                directLog.Start();
                directLogs.Add(directLog);
                Logger.Instance.AttachLogListener(logLevel, directLog);
            }

            if (!logSystem.Contains("DBLog") && !logSystem.Contains("LogJam"))
            {
                // default log type is file log unless otherwise specified
                DirectFileLog fileLog = new DirectFileLog(string.Format("{0}\\{1}.{2}.xml", logInfo, labrunName, assignmentId));
                IDirectLog directLog = new AsyncLog(fileLog);
                directLog.Start();
                directLogs.Add(directLog);
                Logger.Instance.AttachLogListener(logLevel, directLog);
            }

            string realtimelog = configs.ContainsKey("realtimelog") ? configs["realtimelog"] : bool.FalseString;
            bool enableRealTimeLogger = false;
            bool.TryParse(realtimelog, out enableRealTimeLogger);

            if (enableRealTimeLogger)
            {
                Logger.Instance.AttachLogListener(
                    LogLevelType.TestApi | LogLevelType.Test | LogLevelType.ExpWeb | LogLevelType.TFx,
                    RealTimeLogger.Instance);
            }

            // return the directlog object
            return directLogs;
        }

        //private static bool enableRealTimeLogger = false;

        /// <summary>
        /// Helper function to cleanup the logger.  This function auto-detaches 
        /// the direct log from the logger, then cleans up the logger.
        /// </summary>
        /// <param name="directLog">The direct log returned from intialize.</param>
        private static void CleanupLogger(IDirectLog directLog)
        {
            // make sure direct log exists before detaching it.
            if (directLog != null)
            {
                // end the direct log.
                directLog.End();

                // detach it from the logger.
                Logger.Instance.DetachLogListener(directLog);
            }

            if (RealTimeLogger.IsInitialized)
            {
                Logger.Instance.DetachLogListener(RealTimeLogger.Instance);
                RealTimeLogger.Instance.Dispose();
            }
        }

        #endregion

        private static void RunTest(object test, MethodInfo methodInfo)
        {
            // Start timing execution time.
            Stopwatch timer = Stopwatch.StartNew();
            Exception unWrap;

            try
            {
                // Execution starts here.
                Logger.Instance.Write(LogLevelType.TFx, methodInfo.Name + ": Test Execution started");

                // Run setup first.
                InvokeSetUp(test, methodInfo);

                // Run the test.
                Logger.Instance.Write(LogLevelType.TFx, methodInfo.Name + ": Invoke Test");

                InvokeMethod(test, methodInfo);
            }
            catch (Exception e)
            {
                // If the test (or setup) throws any sort of exception, 
                // we mark the test as failed giving the exception's message
                // as the reason why it failed, and the exception's stack trace
                // as the details.
                unWrap = UnwrapException(e);
                TestContext.DriverOnly.MarkTestFailed(UnwrapException(unWrap));
                Logger.Instance.WriteError("Exception when trying to execute test : {0}", unWrap);

            }
            finally
            {
                try
                {
                    // Finally, run teardown.
                    InvokeTearDown(test, methodInfo);
                }
                catch (Exception e)
                {
                    // If the teardown throws any sort of exception, 
                    // we mark the test as failed giving the exception's message
                    // as the reason why it failed, and the exception's stack trace
                    // as the details.
                    unWrap = UnwrapException(e);
                    TestContext.DriverOnly.MarkTestFailed(unWrap);
                    Logger.Instance.WriteError("Exception in tear down : {0}", unWrap);
                }
                finally
                {
                    // Stop the timer.
                    timer.Stop();

                    // Report how long it took to run this test.
                    Logger.Instance.Write(LogLevelType.TestApi, methodInfo.Name + ": Finished execution in {0} seconds.",
                        timer.Elapsed.TotalSeconds);
                }
            }
        }

        /// <summary>
        /// Invoke the test method by getting the parameters from config
        /// if the test method has any parameters
        /// </summary>
        /// <param name="test"></param>
        /// <param name="methodInfo"></param>
        private static void InvokeMethod(object test, MethodInfo methodInfo)
        {
            object[] paramaters = FindMethodParameters(methodInfo);

            methodInfo.Invoke(test, paramaters);
        }


        private static object[] FindMethodParameters(MethodInfo methodInfo)
        {
            ArrayList paramList = new ArrayList();
            string paramValue;
            object[] paramaters;

            foreach (ParameterInfo param in methodInfo.GetParameters())
            {
                paramValue = null;

                if (TestContext.DriverOnly.TestParameters.ContainsKey(param.Name))
                {
                    paramValue = TestContext.DriverOnly.TestParameters[param.Name];
                    ConvertParam(paramValue, param.ParameterType, paramList);
                }
                else
                {
                    Logger.Instance.WriteWarning("Param ({0}) is not specified in the config", param.Name);
                    //Add the paramValue even its null because the parameters count should match in invoke
                    paramList.Add(paramValue);

                }

            }
            paramaters = (object[])(paramList.ToArray());
            return paramaters;
        }

        /// <summary>
        /// Convert the param(string) to the specified parameterType
        /// </summary>
        /// <param name="paramValue"></param>
        /// <param name="parameterType"></param>
        /// <param name="paramList"></param>
        private static void ConvertParam(string paramValue, Type parameterType, ArrayList paramList)
        {
            //Eval paramValue
            paramValue = TestContext.DriverOnly.EvalExpression(paramValue);

            switch (parameterType.ToString().ToLower())
            {
                case "system.string":
                    paramList.Add(paramValue);
                    break;
                case "system.int32":
                    paramList.Add(Convert.ToInt32(paramValue));
                    break;
                case "system.decimal":
                    paramList.Add(Convert.ToDecimal(paramValue));
                    break;
                case "system.boolean":
                    paramList.Add(Convert.ToBoolean(paramValue));
                    break;
                default:
                    if (parameterType.IsEnum == true)
                    {
                        if (Enum.IsDefined(parameterType, paramValue))
                        {
                            paramList.Add(Enum.Parse(parameterType, paramValue));
                        }
                        else
                        {
                            Logger.Instance.WriteError(String.Format("Enum {0} do not contain the value {1}", parameterType, paramValue));
                        }
                    }
                    else
                    {
                        Logger.Instance.WriteError(String.Format("Type {0} is not supported in driver", parameterType));
                    }
                    break;
            }
        }



        private static MethodInfo FindMethodByTestName(string testFullName, string moduleFullName)
        {
            string nameSpace, testName;

            // Parse out namespace and testname
            // Class (Type) name is not in the testFullName (making reflection harder)
            int lastDot = testFullName.LastIndexOf('.');
            if (lastDot < 0)
            {
                throw new TestNotFoundException();
            }

            nameSpace = testFullName.Substring(0, lastDot);
            testName = testFullName.Substring(lastDot + 1);

            Assembly assembly = null;

            try
            {
                assembly = Assembly.LoadFrom(moduleFullName);
            }
            catch (Exception e)
            {
                throw new TestNotFoundException(e);
            }

            // Get all publically visible types and iterate through each one
            Type[] types = assembly.GetExportedTypes();
            foreach (Type type in types)
            {
                // Check for a matching namespace

                if ((!String.IsNullOrEmpty(type.Namespace)) &&
                    (type.Namespace.Equals(nameSpace, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // Try to find test method in type
                    MethodInfo methodInfo = type.GetMethod(
                        testName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase |
                        BindingFlags.InvokeMethod |
                        BindingFlags.DeclaredOnly);
                    if (methodInfo != null)
                    {
                        return methodInfo;
                    }
                }
            }

            throw new TestNotFoundException();
        }

        private static MethodInfo FindMethodByAttribute(object fixture, Type type)
        {
            foreach (MethodInfo method in fixture.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
            {
                if (Util.IsAttributeDefined(method, type))
                {
                    return method;
                }
            }
            return null;
        }

        private static void InvokeSetUp(object fixture, MethodInfo methodInfo)
        {
            MethodInfo method = FindMethodByAttribute(fixture, typeof(SetUpAttribute));
            if (method != null)
            {
                Logger.Instance.Write(LogLevelType.TFx, methodInfo.Name + ": Invoke [SetUp]");
                TestContext.Instance.Scenario("[SetUp]");

                method.Invoke(fixture, null);
                TestContext.Instance.ScenarioFinish();
            }
        }

        private static void InvokeTearDown(object fixture, MethodInfo methodInfo)
        {
            MethodInfo method = FindMethodByAttribute(fixture, typeof(TearDownAttribute));
            if (method != null)
            {
                Logger.Instance.Write(LogLevelType.TFx, methodInfo.Name + ": Invoke [TearDown]");
                TestContext.Instance.Scenario("[TearDown]");

                method.Invoke(fixture, null);
                TestContext.Instance.ScenarioFinish();
            }
        }

        private static Exception UnwrapException(Exception originalException)
        {
            Exception unwrapped = originalException;
            while ((unwrapped is TargetInvocationException) && (unwrapped.InnerException != null))
            {
                unwrapped = unwrapped.InnerException;
            }

            return unwrapped;
        }

        #endregion

        #region List Functions

        public static object[] TestCasesList(string assemblyName)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(assemblyName);
            string EStar = "EStar.dll";
            string ExpTemplate = "ExpTemplates.dll";

            try
            {
                System.Environment.CurrentDirectory = new FileInfo(assemblyName).Directory.FullName;
                Assembly assembly = Assembly.LoadFrom(assemblyName);

                //EStar dll has dependency on ExpTemplate.dll. If we don't load it , it is automatically
                //loading from Driver path
                if (assemblyName.Contains(EStar))
                {
                    Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(assemblyName), ExpTemplate));
                }


                var testMethods = from cl in assembly.GetExportedTypes()
                                  from method in cl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                                  where Util.IsAttributeDefined(method, typeof(TestAttribute))
                                  select new { testFullName = cl.Namespace + "." + method.Name, methodInfo = method };

                var paramList = from testMethod in testMethods
                                select new { TestFullName = testMethod.testFullName, ParamList = GetParamList(testMethod.methodInfo), ConfigList = GetConfigList(testMethod.methodInfo) };

                return paramList.ToArray();

            }
            catch (Exception e)
            {
                Logger.Instance.WriteWarning(e.ToString());
                return null;
            }

        }

        private static object[] GetConfigList(MethodInfo methodInfo)
        {
            List<object> configList = methodInfo.GetCustomAttributes(typeof(ConfigAttribute), false).ToList();

            List<object> classConfigList = methodInfo.DeclaringType.GetCustomAttributes(typeof(ConfigAttribute), false).ToList();

            List<object> assemblyConfigList = methodInfo.DeclaringType.Assembly.GetCustomAttributes(typeof(ConfigAttribute), false).ToList();

            foreach (object cl in classConfigList)
            {
                if (!configList.Contains(cl))
                {
                    configList.Add(cl);
                }
            }

            foreach (object cl in assemblyConfigList)
            {
                if (!configList.Contains(cl))
                {
                    configList.Add(cl);
                }
            }

            var totalConfigList = from cl in configList.Cast<ConfigAttribute>().Distinct(new ConfigAttributeComparer())
                                  select new { Name = cl.Name, DefaultValue = cl.DefaultValue, DataType = "System.String" };

            return totalConfigList.ToArray();

        }

        private static object[] GetParamList(MethodInfo methodInfo)
        {
            var paramList = from pl in methodInfo.GetParameters()
                            select new { Name = pl.Name, DataType = GetParameterType(pl.ParameterType) };

            return paramList.ToArray();
        }

        private static String GetParameterType(Type type)
        {
            if (type.IsEnum)
                return "Enum";
            else
                return type.ToString();
        }

        private static TestModule LoadModule(string assemblyName)
        {
            TestModule module = new TestModule(assemblyName);
            ExecuteTestModule buildModule = new ExecuteTestModule(assemblyName);
            int testFixtureCount = 0;

            try
            {
                Assembly assembly = Assembly.LoadFrom(assemblyName);

                Module[] testType1 = assembly.GetModules();
                Type[] testTypes = assembly.GetExportedTypes();

                foreach (Type testType in testTypes)
                {
                    testFixtureCount++;
                    string namespaces = testType.Namespace;
                    TestArea root = module.RootTestArea.CreateFindTestSuite(namespaces);
                    AddTestCases(buildModule, root, namespaces, testType);
                }

                if (testFixtureCount == 0)
                    throw new ApplicationException(assemblyName + " has no TestFixtures");

            }
            catch (Exception e)
            {
                throw new ModuleNotFoundException(UnwrapException(e));
            }

            return module;

        }

        private static void AddTestWithDuplicateCheck(TestArea area, Type testType, MethodInfo method, TestCase testCase)
        {
            if (area.Tests.ContainsKey(method.Name))
            {
                TestCase test = area.Tests[method.Name] as TestCase;

                test.Disabled = true;
                if (test.DisabledReason == null)
                {
                    test.DisabledReason = "";
                }
                test.DisabledReason += "Duplicate test name in " + testType.FullName + "\n";
            }
            else
            {
                area.Tests.Add(method.Name, testCase);
            }
        }

        private static string FindTestOwner(MethodInfo info)
        {
            // Function level
            if (Util.IsAttributeDefined(info, typeof(TestOwnerAttribute)))
            {
                TestOwnerAttribute attrib = Util.GetAttribute(info, typeof(TestOwnerAttribute)) as TestOwnerAttribute;
                return attrib.Owner;
            }
            else if (info != null)
            {
                // Class level
                if (Util.IsAttributeDefined(info.DeclaringType, typeof(TestOwnerAttribute)))
                {
                    TestOwnerAttribute attrib = Util.GetAttribute(info.DeclaringType, typeof(TestOwnerAttribute)) as TestOwnerAttribute;
                    return attrib.Owner;
                }
                // assembly level
                else if (Util.IsAttributeDefined(info.DeclaringType.Assembly, typeof(TestOwnerAttribute)))
                {
                    TestOwnerAttribute attrib = Util.GetAttribute(info.DeclaringType.Assembly, typeof(TestOwnerAttribute)) as TestOwnerAttribute;
                    return attrib.Owner;
                }

            }

            return null;
        }

        private static void AddTestCases(TestBuildModule module, TestArea area, string parentPath, Type testType)
        {
            MethodInfo[] methods = testType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                if (Util.IsAttributeDefined(method, typeof(TestAttribute)))
                {
                    long category = GetTestCategory(method);
                    string owner = FindTestOwner(method);

                    TestCase testCase = new TestCase(testType.Namespace, method.Name, owner, module, category);
                    AddTestWithDuplicateCheck(area, testType, method, testCase);
                }
            }
        }

        private static long GetTestCategory(MethodInfo info)
        {
            // Function level
            if (Util.IsAttributeDefined(info, typeof(BaseTestCategoryAttribute)))
            {
                Attribute[] attribs = Util.GetAttributes(info, typeof(BaseTestCategoryAttribute));
                return GetCategoryId(attribs);
            }
            else if (info != null)
            {
                // Class level
                if (Util.IsAttributeDefined(info.DeclaringType, typeof(BaseTestCategoryAttribute)))
                {
                    Attribute[] attribs = Util.GetAttributes(info.DeclaringType, typeof(BaseTestCategoryAttribute));
                    return GetCategoryId(attribs);
                }//
                // assembly level
                else if (Util.IsAttributeDefined(info.DeclaringType.Assembly, typeof(BaseTestCategoryAttribute)))
                {
                    Attribute[] attribs = Util.GetAttributes(info.DeclaringType.Assembly, typeof(BaseTestCategoryAttribute));
                    return GetCategoryId(attribs);
                }
            }

            return 0;
        }

        private static long GetCategoryId(Attribute[] attribs)
        {
            long result = 0;
            long longTier = 0;
            foreach (Attribute att in attribs)
            {
                BaseTestCategoryAttribute testCategory = att as BaseTestCategoryAttribute;
                if (testCategory != null)
                {
                    result |= testCategory.CategoryId;
                }
            }

            longTier = (long)TierType.Tier0 | (long)TierType.Tier1 | (long)TierType.Tier2 | (long)TierType.Tier3 | (long)TierType.Tier4;

            if ((result & longTier) != 0)
            {
                result = result | (long)TierType.None;
            }
            return result;
        }

        #endregion
    }
}
