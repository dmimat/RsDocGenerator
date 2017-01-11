using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;

namespace RsDocGenerator
{
    public class FeatureCatalog
    {
        public RsFeatureKind FeatureKind { get; set; }
        public List<string> Languages { get; set; }
        public List<RsFeature> Features { get; set; }

        public FeatureCatalog(RsFeatureKind featureKind)
        {
            FeatureKind = featureKind;
            Languages = new List<string>();
            Features = new List<RsFeature>();
        }

        public void AddFeature(RsFeature feature, string lang)
        {
            if(!Languages.Contains(lang))
                Languages.Add(lang);
            Features.Add(feature);
        }

        public Dictionary<string, List<RsFeature>> GetFeaturesByCategories(string lang)
        {
            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var groups = new Dictionary<string, List<RsFeature>>();
            var groupIds = highlightingManager.ConfigurableGroups.OrderBy(g => g.Title).Select(g => g.Key).ToList();
            groupIds.AddRange(highlightingManager.StaticGroups.OrderBy(g => g.Name).Select(g => g.Key));

            foreach (var group in groupIds)
            {
                var features = Features.Where(f => f.Lang.Equals(lang) && f.GroupId == group).OrderBy(f => f.Text).ToList();
                if (!features.IsEmpty())
                    groups[group] = features;
            }
            return groups;
        }

        public List<RsFeature> GetLangImplementations(string lang)
        {
            return Features.Where(f => f.Lang.Equals(lang)).OrderBy(f => f.Text).ToList();
        }

        public static string GetGroupTitle(string groupId)
        {
            var highlightingManager = Shell.Instance.GetComponent<HighlightingSettingsManager>();
            var staticGroup = highlightingManager.StaticGroups.FirstOrDefault(x => x.Key == groupId);
            if (staticGroup != null)
                return staticGroup.Name;

            var configurableGroup = highlightingManager.ConfigurableGroups.FirstOrDefault(x => x.Key == groupId);
            if (configurableGroup != null)
                return configurableGroup.Title;

            return groupId;
        }
    }
}