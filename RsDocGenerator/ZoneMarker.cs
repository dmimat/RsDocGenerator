using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Resources.Shell;

namespace RsDocGenerator
{
    [ZoneMarker]
    public class ZoneMarker : IRequire<PsiFeaturesImplZone>, IRequire<ILanguageCSharpZone>, IRequire<DaemonZone>, IRequire<ILanguageCppZone>
    {
    }
}