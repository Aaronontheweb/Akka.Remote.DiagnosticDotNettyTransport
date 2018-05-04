using System.Reflection;
using Akka.Configuration;

namespace Akka.Remote.DiagnosticDotNettyTransport.Configuration
{
    /// <summary>
    /// Exposes the default HOCON configuration for working with the <see cref="DiagnosticDotNettyTransport"/>.
    /// </summary>
    public static class DiagnosticConfig
    {
        /// <summary>
        /// The default configuration for the <see cref="DiagnosticDotNettyTransport"/>
        /// </summary>
        public static Akka.Configuration.Config DefaultDiagnosticConfiguration { get; }

        static DiagnosticConfig()
        {
            DefaultDiagnosticConfiguration =
                ConfigurationFactory.FromResource(
                    "Akka.Remote.DiagnosticDotNettyTransport.Configuration.dotnetty.diagnostic.conf", 
                    typeof(DiagnosticConfig).GetTypeInfo().Assembly);
        }
    }
}
