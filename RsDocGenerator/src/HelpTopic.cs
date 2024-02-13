using System.IO;
using System.Xml.Linq;

namespace RsDocGenerator
{
    public class HelpTopic
    {
        private readonly XDocument topicDocument;
        private readonly string topicId;
        private readonly string topicPath;

        public HelpTopic(string id, string title, string path)
        {
            topicId = id;
            topicPath = path;
            topicDocument = new XDocument(
                new XDocumentType("topic", null, "https://resources.jetbrains.com/writerside/1.0/html-entities.dtd", 
                    null));
            topicDocument.Add(new XElement("topic"));

            XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
            topicDocument.Root.Add(
                new XAttribute(xsiNs + "noNamespaceSchemaLocation",
                    "https://resources.jetbrains.com/writerside/1.0/topic.v2.xsd"),
                new XAttribute(XNamespace.Xmlns + "xsi", xsiNs),
                new XAttribute("id", id),
                new XAttribute("title", title));

            XmlHelpers.AddAutoGenComment(topicDocument.Root);
            topicDocument.Root.Add(XmlHelpers.CreateInclude("GEN", id + "_start", true));
        }

        public void Save()
        {
            topicDocument.Save(Path.Combine(topicPath, topicId + ".topic"));
        }

        public void Add(object content)
        {
            topicDocument.Root.Add(content);
        }

        public XElement GetRoot()
        {
            return topicDocument.Root;
        }
    }
}