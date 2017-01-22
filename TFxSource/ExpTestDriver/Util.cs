using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Expedia.Test.Framework
{
    /// <summary>
    /// Summary description for Util.
    /// </summary>
    public class Util
    {
        public static Attribute[] GetAttributes(MethodInfo type, Type attribute)
        {
            List<Attribute> attribs = new List<Attribute>();

            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                Attribute myAtt = att as Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType().Name == attribute.Name || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        attribs.Add(myAtt);
                    }
                }
            }

            return attribs.ToArray();
        }

        public static Attribute[] GetAttributes(Assembly assembly, Type attribute)
        {
            List<Attribute> attribs = new List<Attribute>();
            object[] atts = assembly.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType().Name == attribute.Name || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        attribs.Add(myAtt);
                    }
                }
            }

            return attribs.ToArray();
        }

        public static Attribute[] GetAttributes(Type type, Type attribute)
        {
            List<Attribute> attribs = new List<Attribute>();

            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType().Name == attribute.Name || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        attribs.Add(myAtt);
                    }
                }
            }

            return attribs.ToArray();
        }

        public static Attribute GetAttribute(MethodInfo type, Type attribute)
        {
            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType() == attribute || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        return myAtt;
                    }
                }
            }
            return null;
        }

        public static Attribute GetAttribute(Type type, Type attribute)
        {
            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType() == attribute || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        return myAtt;
                    }
                }
            }
            return null;
        }

        public static Attribute GetAttribute(Assembly assembly, Type attribute)
        {
            object[] atts = assembly.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (myAtt.GetType() == attribute || myAtt.GetType().IsSubclassOf(attribute))
                    {
                        return myAtt;
                    }
                }
            }
            return null;
        }

        public static bool IsAttributeDefined(Assembly assembly, Type attribute)
        {
            if (assembly.IsDefined(attribute, true))
            {
                return true;
            }

            //TODO:Fix the hack as soon as i move Attributes into same dll IsDefined always returing false
            object[] atts = assembly.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (string.Compare(name, attribute.FullName, true) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsAttributeDefined(Type type, Type attribute)
        {
            if (type.IsAbstract)
            {
                return false;
            }

            if (type.IsDefined(attribute, true))
            {
                return true;
            }

            //TODO:Fix the hack as soon as i move Attributes into same dll IsDefined always returing false
            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (string.Compare(name, attribute.FullName, true) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsAttributeDefined(MethodInfo type, Type attribute)
        {
            if (type.IsAbstract)
            {
                return false;
            }

            if (type.IsDefined(attribute, true))
            {
                return true;
            }

            //TODO:Fix the hack as soon as i move Attributes into same dll IsDefined always returing false
            object[] atts = type.GetCustomAttributes(true);
            foreach (object att in atts)
            {
                System.Attribute myAtt = att as System.Attribute;
                if (attribute != null)
                {
                    string name = myAtt.ToString();
                    if (string.Compare(name, attribute.FullName, true) == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
