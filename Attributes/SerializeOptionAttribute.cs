using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TA.Pahit.Helpers.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SerializeOptionAttribute : Attribute
    {
        public bool SerializeAll { get; }
        public string Name { get; }

        public AttributeType PreferedAttributeType { get; }
        public SerializeOptionAttribute(bool serializeAll = true, string name = null, AttributeType preferredType = AttributeType.Element)
        {
            PreferedAttributeType = preferredType;
            SerializeAll = serializeAll;
            Name = name;
        }
    }
}
