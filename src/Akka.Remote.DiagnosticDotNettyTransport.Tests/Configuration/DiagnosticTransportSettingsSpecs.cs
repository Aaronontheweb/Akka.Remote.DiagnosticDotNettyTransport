using System;
using Akka.Remote.DiagnosticDotNettyTransport.Configuration;
using DotNetty.Common;
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

        [Theory(DisplayName = "Should be able to parse ResourceLeakDetector.DetectionLevel from HOCON")]
        [InlineData(ResourceLeakDetector.DetectionLevel.Paranoid)]
        [InlineData(ResourceLeakDetector.DetectionLevel.Advanced)]
        [InlineData(ResourceLeakDetector.DetectionLevel.Simple)]
        [InlineData(ResourceLeakDetector.DetectionLevel.Disabled)]
        public void ShouldParseResourceLeakDetectorLevel(ResourceLeakDetector.DetectionLevel detectionLevel)
        {
            var parsedLevel =
                DiagnosticDotNettyTransportSettings.ExtractLeakDetectionLevelFromConfig(detectionLevel.ToString());
            parsedLevel.Should().Be(detectionLevel);
        }

        [Fact(DisplayName = "Default DiagnosticDotNettyTransportSettings should be correct")]
        public void ShouldLoadDefaultDiagnosticConfigSettings()
        {
            var c = DiagnosticConfig.DefaultDiagnosticConfiguration.GetConfig("akka.remote.dot-netty.diagnostic.tcp");
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

            // Test diagnostic settings
            s.ResourceLeakDetectionLevel.Should().Be(ResourceLeakDetector.DetectionLevel.Simple);
            s.EnableBufferPoolDumps.Should().BeTrue();
            s.BufferPoolDumpSampleRate.Should().Be(1.0d);
            s.CaptureDotNettyLogs.Should().BeTrue();
        }
    }
}
