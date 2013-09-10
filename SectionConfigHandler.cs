using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Xml;

namespace XOGroup.Image.IO
{
    public class SectionConfigHandler : IConfigurationSectionHandler
    {
        #region IConfigurationSectionHandler Members

        public object Create(object parent, object configContext, XmlNode section)
        {
            IDictionary<string, IDictionary<string, string>> sectionConfig = new Dictionary<string, IDictionary<string, string>>();
            XmlAttribute name = null;
            XmlAttribute value = null;

            foreach (XmlNode node in section.ChildNodes)
            {
                sectionConfig.Add(node.Name, new Dictionary<string, string>());
                foreach (XmlNode subNode in node.ChildNodes)
                {
                    if (subNode.NodeType == XmlNodeType.Element)
                    {
                        value = subNode.Attributes["value"];
                        name = subNode.Attributes["name"] ?? value;
                        sectionConfig[node.Name].Add(name.Value, value.Value);
                    }
                }
            }

            return sectionConfig;
        }

        #endregion
    }
}
