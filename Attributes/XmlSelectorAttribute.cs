using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TA.Pahit.Helpers.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class XmlSelectorAttribute : Attribute
    {
        public AttributeType AttributeType { get; }
        public bool Ignore { get; }
        public string Name { get; }
        public string Groups { get; }

        public XmlSelectorAttribute(AttributeType attributeType = AttributeType.Element, string name = null,
            bool ignore = false, string groups = "")
        {
            AttributeType = attributeType;
            Ignore = ignore;
            Name = name;
            Groups = groups;
        }
    }

    public enum AttributeType
    {
        Attribut,
        Element
    }
}