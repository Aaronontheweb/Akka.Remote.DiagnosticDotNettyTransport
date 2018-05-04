using Akka.Remote.DiagnosticDotNettyTransport.Configuration;
using FluentAssertions;
using Xunit;

namespace Akka.Remote.DiagnosticDotNettyTransport.Tests.Configuration
{
    public class DiagnosticTransportSettingsSpecs
    {
        [Fact(DisplayName = "Should load diagnostic config")]
        public void ShouldLoadDiagnosticConfig()
        {
            DiagnosticConfig.DefaultDiagnosticConfiguration.Should().NotBeNull();
        }
    }
}
