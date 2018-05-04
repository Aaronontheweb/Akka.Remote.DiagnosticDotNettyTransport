using System;
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

        [Fact(DisplayName = "Default DiagnosticDotNettyTransportSettings should be correct")]
        public void ShouldLoadDefaultDiagnosticConfigSettings()
        {
            var c = DiagnosticConfig.DefaultDiagnosticConfiguration;
            var s = DiagnosticDotNettyTransportSettings.Create(c);

            Assert.Equal(TimeSpan.FromSeconds(15), s.ConnectTimeout);
            Assert.Null(s.WriteBufferHighWaterMark);
            Assert.Null(s.WriteBufferLowWaterMark);
            Assert.Equal(256000, s.SendBufferSize.Value);
            Assert.Equal(256000, s.ReceiveBufferSize.Value);
            Assert.Equal(128000, s.MaxFrameSize);
            Assert.Equal(4096, s.Backlog);
            Assert.True(s.TcpNoDelay);
            Assert.True(s.TcpKeepAlive);
            Assert.True(s.TcpReuseAddr);
            Assert.True(string.IsNullOrEmpty(c.GetString("hostname")));
            Assert.Null(s.PublicPort);
            Assert.Equal(2, s.ServerSocketWorkerPoolSize);
            Assert.Equal(2, s.ClientSocketWorkerPoolSize);
            Assert.False(s.BackwardsCompatibilityModeEnabled);
            Assert.False(s.DnsUseIpv6);
            Assert.False(s.LogTransport);
            Assert.False(s.EnableSsl);
        }
    }
}
