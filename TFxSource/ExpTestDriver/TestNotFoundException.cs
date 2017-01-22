using System;

namespace Expedia.Test.Framework
{
    /// <summary>
    /// Summary description for TestNotFoundException.
    /// </summary>
    public class TestNotFoundException : TFxException
    {
        public TestNotFoundException()
            : base("Test not found")
        {
        }
        public TestNotFoundException(Exception e)
            : base("Test not found", e)
        {
        }
    }
}
