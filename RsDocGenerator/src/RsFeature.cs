using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Daemon;

namespace RsDocGenerator
{
    public class RsFeature
    {
        public RsFeature([NotNull]string id, string text,
            string lang, List<string> multilang, RsFeatureKind kind, Severity severity, string compoundName, string groupId)
        {
            if (text == null)
                text = id;
            if(multilang == null)
                multilang = new List<string>{lang};
            Id = id;
            Text = text;
            Lang = lang;
            Multilang = multilang;
            Kind = kind;
            Severity = severity;
            CompoundName = compoundName;
            GroupId = groupId;
        }

        public string Id { get; set; }
        public string Text { get; set; }
        public string GroupId { get; set; }
        public string Lang { get; set; }
        public List<string> Multilang { get; set; }
        public RsFeatureKind Kind { get; set; }
        public Severity Severity { get; set; }
        public string CompoundName { get; set; }
    }
}