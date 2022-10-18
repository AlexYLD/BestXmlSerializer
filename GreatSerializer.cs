using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using TA.Pahit.Helpers.Attributes;

namespace TA.Pahit.Helpers
{
    public class GreatSerializer
    {
        public static string Serialize<T>(T subject, string rootName, List<string> groups = null)
        {
            StringBuilder sb = new StringBuilder("<").Append(rootName).Append(" auto=\"true\"");
            if (groups == null) groups = new List<string>();
            Serialize(subject, sb, 1, new Dictionary<object, string>(), groups);
            sb.Append("</").Append(rootName).Append(">");
            return sb.ToString();
        }

        public static string AddXmlHeader(string content, string version = "1.0", string encoding = "utf-8")
        {
            return $"<?xml version=\"{version}\" encoding=\"{encoding}\"?>\n" + content;
        }

        private static void Serialize<T>(T subject, StringBuilder sb, int tabLevel,
            Dictionary<object, string> usedObjects, List<string> groups)
        {
            if (subject == null) {
                sb.Append(">\n");
                return;
            }

            if (subject is Type) {
                sb.Append(">");
                sb.Append(ReplaceIllegalCharacters((subject as Type).AssemblyQualifiedName));
                return;
            }

            if (IsSimple(subject.GetType())) {
                sb.Append(">");
                sb.Append(ReplaceIllegalCharacters((subject.ToString())));
                return;
            }

            SerializeOptionAttribute classAttribute;
            if (subject is ICollection collection) {
                sb.Append(">");
                if (collection.Count == 0) return;
                sb.Append("\n");
                if (collection is IDictionary dictionary) {
                    foreach (DictionaryEntry entry in dictionary) {
                        AppendTabs(sb, tabLevel);
                        sb.Append("<Entry>\n");

                        AppendTabs(sb, tabLevel + 1);
                        sb.Append("<Key");
                        Serialize(entry.Key, sb, tabLevel + 2, usedObjects, groups);
                        if (sb[sb.Length - 1].Equals('\n')) AppendTabs(sb, tabLevel + 2);
                        sb.Append("</Key>\n");

                        AppendTabs(sb, tabLevel + 1);
                        sb.Append("<Value");
                        Serialize(entry.Value, sb, tabLevel + 2, usedObjects, groups);
                        if (sb[sb.Length - 1].Equals('\n')) AppendTabs(sb, tabLevel + 1);
                        sb.Append("</Value>\n");

                        if (sb[sb.Length - 1].Equals('\n')) AppendTabs(sb, tabLevel);
                        sb.Append("</Entry>\n");
                    }

                    return;
                }

                foreach (var subItem in collection) {
                    if (IsSimple(subItem.GetType())) {
                        AppendTabs(sb, tabLevel + 1);
                        sb.Append("<value>").Append(ReplaceIllegalCharacters(subItem.ToString())).Append("</value>\n");
                    }
                    else {
                        classAttribute = (SerializeOptionAttribute)Attribute.GetCustomAttribute(
                            collection.GetType().GetGenericArguments().Single(),
                            typeof(SerializeOptionAttribute));
                        if (classAttribute == null) continue;
                        AppendTabs(sb, tabLevel);
                        string name = classAttribute.Name ?? subItem.GetType().Name;
                        sb.Append("<").Append(ReplaceIllegalCharacters(name));
                        Serialize(subItem, sb, tabLevel + 1, usedObjects, groups);
                        if (sb[sb.Length - 1].Equals('\n')) AppendTabs(sb, tabLevel);
                        sb.Append("</").Append(ReplaceIllegalCharacters(name)).Append(">\n");
                    }
                }

                return;
            }

            classAttribute = (SerializeOptionAttribute)Attribute.GetCustomAttribute(subject.GetType(),
                typeof(SerializeOptionAttribute));
            if (classAttribute == null) {
                sb.Append(">");
                return;
            }

            Dictionary<PropertyInfo, XmlSelectorAttribute> props =
                GetXmlSelectedProperties(subject.GetType(), classAttribute, groups);

            List<PropertyInfo> xmlAttributes = props.Where(p => p.Value.AttributeType.Equals(AttributeType.Attribut))
                .Select(p => p.Key).ToList();
            foreach (PropertyInfo propertyInfo in xmlAttributes) {
                var value = propertyInfo.GetValue(subject);
                if (value == null) continue;
                string name = props[propertyInfo].Name ?? propertyInfo.Name;
                string strValue;
                if (value is ICollection arr) {
                    strValue = "[" + string.Join(",", arr.Cast<object>()) + "]";
                }
                else if (value is Type type) {
                    strValue = type.AssemblyQualifiedName;
                }
                else {
                    strValue = value.ToString();
                }

                sb.Append(" ").Append(ReplaceIllegalCharacters(name)).Append("=\"").Append(ReplaceIllegalCharacters(strValue)).Append("\"");
            }

            List<PropertyInfo> xmlElements = props.Where(p => p.Value.AttributeType.Equals(AttributeType.Element))
                .Select(p => p.Key).ToList();

            sb.Append(">");
            if (xmlElements.Count > 0) sb.Append("\n");
            if (xmlAttributes.Count == 0 && xmlElements.Count == 0) {
                if (!subject.ToString().Equals(subject.GetType().ToString())) sb.Append(subject);
            }

            foreach (PropertyInfo propertyInfo in xmlElements) {
                var value = propertyInfo.GetValue(subject);
                if (value == null) continue;
                AppendTabs(sb, tabLevel);
                string name = props[propertyInfo].Name ?? propertyInfo.Name;
                sb.Append("<").Append(ReplaceIllegalCharacters(name));
                if (usedObjects.ContainsKey(value)) {
                    sb.Append("SerId=").Append(usedObjects[value]).Append("/>");
                }
                else {
                    Serialize(value, sb, tabLevel + 1, usedObjects, groups);
                    if (sb[sb.Length - 1].Equals('\n')) AppendTabs(sb, tabLevel);
                    sb.Append("</").Append(ReplaceIllegalCharacters(name)).Append(">\n");
                }
            }
        }

        public static T Deserialize<T>(string xmlString, List<string> groups = null)
        {
            if (string.IsNullOrEmpty(xmlString)) return default(T);
            XDocument doc = XDocument.Parse(xmlString);
            if (groups == null) groups = new List<string>();

            var result = Deserialize<T>(doc.Root, groups);
            return result;
        }

        private static T Deserialize<T>(XElement xElement, List<string> groups)
        {
            if (xElement == null) {
                return default(T);
            }

            if (IsSimple(typeof(T))) {
                return (T)Convert.ChangeType(xElement.Value, typeof(T));
            }

            if (typeof(T) == typeof(Type)) {
                return (T)(object)Type.GetType(xElement.Value);
            }


            if (typeof(T).GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))) {
                if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                    Type[] types = typeof(T).GetGenericArguments();
                    Type keyType = types[0];
                    Type valueType = types[1];
                    IDictionary dict = (IDictionary)Activator.CreateInstance(typeof(T).IsInterface
                        ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
                        : typeof(T));

                    foreach (XElement entryElement in xElement.Elements("Entry")) {
                        var key = GetDeserializeMethod(keyType).Invoke(null, new object[] { entryElement.Element("Key"), groups });
                        var value = GetDeserializeMethod(valueType).Invoke(null, new object[] { entryElement.Element("Value"), groups });
                        dict.Add(key, value);
                    }

                    return (T)dict;
                }

                Type listGenericType = typeof(T).GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(typeof(T).IsInterface
                    ? typeof(List<>).MakeGenericType(listGenericType)
                    : typeof(T));

                foreach (XElement itemElement in xElement.Elements()) {
                    Type itemType = null;
                    if (listGenericType.Name != itemElement.Name.ToString()) {
                        itemType = Assembly.GetAssembly(listGenericType).GetTypes().FirstOrDefault(t => t.IsSubclassOf(listGenericType) && t.Name == itemElement.Name.ToString());
                    }

                    if (itemType == null) itemType = listGenericType;
                    var item = GetDeserializeMethod(itemType).Invoke(null, new object[] { itemElement, groups });
                    list.Add(item);
                }

                return (T)list;
            }

            var classAttribute = (SerializeOptionAttribute)Attribute.GetCustomAttribute(typeof(T),
                typeof(SerializeOptionAttribute));

            if (classAttribute == null) {
                return default(T);
            }

            Dictionary<PropertyInfo, XmlSelectorAttribute> props =
                GetXmlSelectedProperties(typeof(T), classAttribute, groups);

            T result = Activator.CreateInstance<T>();

            foreach (var propertyInfo in props.Keys) {
                XmlSelectorAttribute selctor = props[propertyInfo];
                string xmlName = props[propertyInfo].Name ?? propertyInfo.Name;

                if (selctor.AttributeType.Equals(AttributeType.Attribut)) {
                    XAttribute attr = xElement.Attribute(xmlName);
                    if (attr == null) continue;

                    if (propertyInfo.PropertyType.GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))) {
                        var listValue = attr.Value.Substring(1, attr.Value.Length - 2).Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                        propertyInfo.SetValue(result, Activator.CreateInstance(propertyInfo.PropertyType, listValue));
                        continue;
                    }

                    if (propertyInfo.PropertyType == typeof(Type)) {
                        propertyInfo.SetValue(result, Type.GetType(attr.Value));
                        continue;
                    }

                    if (propertyInfo.PropertyType.IsEnum) {
                        propertyInfo.SetValue(result, Enum.Parse(propertyInfo.PropertyType, attr.Value));
                        continue;
                    }

                    propertyInfo.SetValue(result, Convert.ChangeType(attr.Value, propertyInfo.PropertyType));
                    continue;
                }

                XElement element = xElement.Element(xmlName);
                if (element == null) continue;
                var thisMethod = GetDeserializeMethod(propertyInfo.PropertyType);
                propertyInfo.SetValue(result, thisMethod.Invoke(null, new object[] { element, groups }));
            }

            return result;
        }

        private static MethodInfo GetDeserializeMethod(Type type)
        {
            MethodInfo thisMethod =
                typeof(GreatSerializer).GetMethod("Deserialize", BindingFlags.NonPublic | BindingFlags.Static);
            thisMethod = thisMethod.MakeGenericMethod(type);
            return thisMethod;
        }


        private static Dictionary<PropertyInfo, XmlSelectorAttribute> GetXmlSelectedProperties(Type type,
            SerializeOptionAttribute classAttribute, List<string> groups)
        {
            Dictionary<PropertyInfo, XmlSelectorAttribute> props = new Dictionary<PropertyInfo, XmlSelectorAttribute>();
            foreach (PropertyInfo property in type.GetProperties()) {
                XmlSelectorAttribute xmlSelectorAttribute = property.GetCustomAttribute<XmlSelectorAttribute>(true);

                if (classAttribute.SerializeAll) {
                    if (xmlSelectorAttribute == null) {
                        xmlSelectorAttribute =
                            new XmlSelectorAttribute(classAttribute.PreferedAttributeType, groups: string.Join(";", groups));
                    }
                    else if (xmlSelectorAttribute.Ignore && IsPropertyInSelectedGroups(xmlSelectorAttribute.Groups, groups)) {
                        continue;
                    }

                    props[property] = xmlSelectorAttribute;
                }
                else {
                    if (xmlSelectorAttribute != null && !xmlSelectorAttribute.Ignore && IsPropertyInSelectedGroups(xmlSelectorAttribute.Groups, groups)) {
                        props[property] = xmlSelectorAttribute;
                    }
                }
            }

            return props;
        }

        private static bool IsPropertyInSelectedGroups(string selectorGroups, List<string> neededGroups)
        {
            if (string.IsNullOrEmpty(selectorGroups)) return true;
            return selectorGroups.Split(';').Intersect(neededGroups).Any();
        }


        private static void AppendTabs(StringBuilder sb, int tabLevel)
        {
            for (int i = 0; i < tabLevel; i++) {
                sb.Append("\t");
            }
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive || type == typeof(string);
        }

        private static string ReplaceIllegalCharacters(string str)
        {
            StringBuilder sb = new StringBuilder(str);
            sb.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;");
            return sb.ToString();
        }
    }
}