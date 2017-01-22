using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Reflection;

namespace Expedia.Test.Framework
{
    public class DriverDomainSafe
    {
        public DriverDomainSafe()
        {
        }

        public static TestArea List(string path)
        {

            TestArea testArea = null;
            FileInfo file = new FileInfo(path);
            AppDomain testDomain = null;

            try
            {
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = file.DirectoryName;
                setup.ApplicationName = "Driver";

                Evidence baseEvidence = AppDomain.CurrentDomain.Evidence;
                Evidence evidence = new Evidence(baseEvidence);

                string domainName = String.Format("{0}-TestDomain", file.Name);
                testDomain = AppDomain.CreateDomain(domainName, evidence, setup);
                testDomain.InitializeLifetimeService();

                testDomain.SetData("path", path);

                testDomain.DoCallBack(new CrossAppDomainDelegate(Driver.SafeList));

                testArea = (TestArea)testDomain.GetData("testArea");
            }
            finally
            {
                if (testDomain != null)
                {
                    AppDomain.Unload(testDomain);
                    testDomain = null;
                }               
            }

            return testArea;
    }

        public void Execute(string testFullName, string moduleFullName, Dictionary<string, string> configs, Dictionary<string, string> testParameters, out string[,] scenarios, out string failureReason, out string assignmentRunResult)
        {
            FileInfo file = new FileInfo(moduleFullName);
            AppDomain testDomain = null;

            scenarios = new string[0, 0];
            failureReason = String.Empty;
            assignmentRunResult = String.Empty;

            try
            {
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = file.DirectoryName;
                setup.ApplicationName = "Driver";

                Evidence baseEvidence = AppDomain.CurrentDomain.Evidence;
                Evidence evidence = new Evidence(baseEvidence);

                string domainName = String.Format("{0}-TestDomain", file.Name);
                testDomain = AppDomain.CreateDomain(domainName, evidence, setup);
                testDomain.InitializeLifetimeService();

                testDomain.SetData("testFullName", testFullName);
                testDomain.SetData("moduleFullName", moduleFullName);
                testDomain.SetData("configs", configs);
                testDomain.SetData("testParameters", testParameters);
                testDomain.SetData("scenarios", scenarios);
                testDomain.SetData("failureReason", failureReason);
                testDomain.SetData("assignmentRunResult", assignmentRunResult);

                scenarios = (string[,])testDomain.GetData("scenarios");
                failureReason = (string)testDomain.GetData("failureReason");
                assignmentRunResult = (string)testDomain.GetData("assignmentRunResult");

                testDomain.DoCallBack(new CrossAppDomainDelegate(Driver.SafeExecute));

                scenarios = (string[,])testDomain.GetData("scenarios");
                failureReason = (string)testDomain.GetData("failureReason");
                assignmentRunResult = (string)testDomain.GetData("assignmentRunResult");

            }
            catch (Exception e)
            {
                failureReason = e.ToString();
            }
            finally
            {
                try
                {
                    if (testDomain != null)
                    {
                        AppDomain.Unload(testDomain);
                        testDomain = null;
                    }
                }
                catch (Exception ex)
                {
                    if (failureReason == null)
                    {
                        failureReason = string.Empty;
                    }
                    failureReason += string.Format("\n\r=========== Driver Exception in finally block =======\n\r{0}", ex.ToString());
                }

            }
        }
    }
}
