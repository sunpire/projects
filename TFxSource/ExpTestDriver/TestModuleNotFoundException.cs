using System;

namespace Expedia.Test.Framework
{
    /// <summary>
    /// Summary description for ModuleNotFoundException.
    /// </summary>
    public class ModuleNotFoundException : TFxException
    {
        public ModuleNotFoundException(Exception e)
            : base("Module not found", e)
        {
        }
    }
}
