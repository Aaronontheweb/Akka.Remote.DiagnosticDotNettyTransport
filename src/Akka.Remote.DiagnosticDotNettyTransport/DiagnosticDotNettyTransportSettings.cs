﻿//-----------------------------------------------------------------------
// <copyright file="DiagnosticDotNettyTransportSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2018 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2018 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Event;
using Akka.Remote.DiagnosticDotNettyTransport.Handlers;
using Akka.Util;
using DotNetty.Buffers;
using DotNetty.Common;

namespace Akka.Remote.DiagnosticDotNettyTransport
{
    /// <summary>
    /// INTERNAL API.
    /// 
    /// Defines the settings for the <see cref="Akka.Remote.DiagnosticDotNettyTransport"/>.
    /// </summary>
    internal sealed class DiagnosticDotNettyTransportSettings
    {
        public static DiagnosticDotNettyTransportSettings Create(ActorSystem system)
        {
            return Create(system.Settings.Config.GetConfig("akka.remote.dot-netty.diagnostic.tcp"));
        }

        public static DiagnosticDotNettyTransportSettings Create(Config config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config), "DotNetty HOCON config was not found (default path: `akka.remote.dot-netty.diagnostic.tcp`)");

            var transportMode = config.GetString("transport-protocol", "tcp").ToLower();
            var host = config.GetString("hostname");
            if (string.IsNullOrEmpty(host)) host = IPAddress.Any.ToString();
            var publicHost = config.GetString("public-hostname", null);
            var publicPort = config.GetInt("public-port", 0);

            var order = ByteOrder.LittleEndian;
            var byteOrderString = config.GetString("byte-order", "little-endian").ToLowerInvariant();
            switch (byteOrderString)
            {
                case "little-endian": order = ByteOrder.LittleEndian; break;
                case "big-endian": order = ByteOrder.BigEndian; break;
                default: throw new ArgumentException($"Unknown byte-order option [{byteOrderString}]. Supported options are: big-endian, little-endian.");
            }

            var resourceLeakConfig =
                ExtractLeakDetectionLevelFromConfig(config.GetString("resource-leak-level", "simple"));

            return new DiagnosticDotNettyTransportSettings(
                transportMode: transportMode == "tcp" ? TransportMode.Tcp : TransportMode.Udp,
                enableSsl: config.GetBoolean("enable-ssl", false),
                connectTimeout: config.GetTimeSpan("connection-timeout", TimeSpan.FromSeconds(15)),
                hostname: host,
                publicHostname: !string.IsNullOrEmpty(publicHost) ? publicHost : host,
                port: config.GetInt("port", 2552),
                publicPort: publicPort > 0 ? publicPort : (int?)null,
                serverSocketWorkerPoolSize: ComputeWorkerPoolSize(config.GetConfig("server-socket-worker-pool")),
                clientSocketWorkerPoolSize: ComputeWorkerPoolSize(config.GetConfig("client-socket-worker-pool")),
                maxFrameSize: ToNullableInt(config.GetByteSize("maximum-frame-size")) ?? 128000,
                ssl: config.HasPath("ssl") ? SslSettings.Create(config.GetConfig("ssl")) : SslSettings.Empty,
                dnsUseIpv6: config.GetBoolean("dns-use-ipv6", false),
                tcpReuseAddr: config.GetBoolean("tcp-reuse-addr", true),
                tcpKeepAlive: config.GetBoolean("tcp-keepalive", true),
                tcpNoDelay: config.GetBoolean("tcp-nodelay", true),
                backlog: config.GetInt("backlog", 4096),
                enforceIpFamily: RuntimeDetector.IsMono || config.GetBoolean("enforce-ip-family", false),
                receiveBufferSize: ToNullableInt(config.GetByteSize("receive-buffer-size") ?? 256000),
                sendBufferSize: ToNullableInt(config.GetByteSize("send-buffer-size") ?? 256000),
                writeBufferHighWaterMark: ToNullableInt(config.GetByteSize("write-buffer-high-water-mark")),
                writeBufferLowWaterMark: ToNullableInt(config.GetByteSize("write-buffer-low-water-mark")),
                backwardsCompatibilityModeEnabled: config.GetBoolean("enable-backwards-compatibility", false),
                logTransport: config.HasPath("log-transport") && config.GetBoolean("log-transport"),
                byteOrder: order,
                enableBufferPooling: config.GetBoolean("enable-pooling", true),
                resourceLeakDetectionLevel: resourceLeakConfig,
                enableBufferPoolDumps: config.GetBoolean("allocator-dumps.enabled", true),
                bufferPoolDumpSampleRate: config.GetDouble("allocator-dumps.sample-rate", 1.0d),
                captureDotNettyLogs: config.GetBoolean("capture-dotnetty-logs", true));
        }

        private static int? ToNullableInt(long? value) => value.HasValue && value.Value > 0 ? (int?)value.Value : null;

        private static int ComputeWorkerPoolSize(Config config)
        {
            if (config == null) return ThreadPoolConfig.ScaledPoolSize(2, 1.0, 2);

            return ThreadPoolConfig.ScaledPoolSize(
                floor: config.GetInt("pool-size-min"),
                scalar: config.GetDouble("pool-size-factor"),
                ceiling: config.GetInt("pool-size-max"));
        }

        internal static ResourceLeakDetector.DetectionLevel ExtractLeakDetectionLevelFromConfig(string parsedConfig)
        {
            switch (parsedConfig.ToLowerInvariant())
            {
                case "disabled":
                    return ResourceLeakDetector.DetectionLevel.Disabled;
                case "simple":
                    return ResourceLeakDetector.DetectionLevel.Simple;
                case "advanced":
                    return ResourceLeakDetector.DetectionLevel.Advanced;
                case "paranoid":
                    return ResourceLeakDetector.DetectionLevel.Paranoid;
                default:
                    throw new ConfigurationException($"Unsupported ResourceLeakDetector.DetectionLevel {parsedConfig}");
            }
        }

        /// <summary>
        /// Transport mode used by underlying socket channel. 
        /// Currently only TCP is supported.
        /// </summary>
        public readonly TransportMode TransportMode;

        /// <summary>
        /// If set to true, a Secure Socket Layer will be established
        /// between remote endpoints. They need to share a X509 certificate
        /// which path is specified in `akka.remote.dot-netty.tcp.ssl.certificate.path`
        /// </summary>
        public readonly bool EnableSsl;

        /// <summary>
        /// Sets a connection timeout for all outbound connections 
        /// i.e. how long a connect may take until it is timed out.
        /// </summary>
        public readonly TimeSpan ConnectTimeout;

        /// <summary>
        /// The hostname or IP to bind the remoting to.
        /// </summary>
        public readonly string Hostname;

        /// <summary>
        /// If this value is set, this becomes the public address for the actor system on this
        /// transport, which might be different than the physical ip address (hostname)
        /// this is designed to make it easy to support private / public addressing schemes
        /// </summary>
        public readonly string PublicHostname;

        /// <summary>
        /// The default remote server port clients should connect to.
        /// Default is 2552 (AKKA), use 0 if you want a random available port
        /// This port needs to be unique for each actor system on the same machine.
        /// </summary>
        public readonly int Port;

        /// <summary>
        /// If this value is set, this becomes the public port for the actor system on this
        /// transport, which might be different than the physical port
        /// this is designed to make it easy to support private / public addressing schemes
        /// </summary>
        public readonly int? PublicPort;

        public readonly int ServerSocketWorkerPoolSize;
        public readonly int ClientSocketWorkerPoolSize;
        public readonly int MaxFrameSize;
        public readonly SslSettings Ssl;

        /// <summary>
        /// If set to true, we will use IPv6 addresses upon DNS resolution for 
        /// host names. Otherwise IPv4 will be used.
        /// </summary>
        public readonly bool DnsUseIpv6;

        /// <summary>
        /// Enables SO_REUSEADDR, which determines when an ActorSystem can open
        /// the specified listen port (the meaning differs between *nix and Windows).
        /// </summary>
        public readonly bool TcpReuseAddr;

        /// <summary>
        /// Enables TCP Keepalive, subject to the O/S kernel's configuration.
        /// </summary>
        public readonly bool TcpKeepAlive;

        /// <summary>
        /// Enables the TCP_NODELAY flag, i.e. disables Nagle's algorithm
        /// </summary>
        public readonly bool TcpNoDelay;

        /// <summary>
        /// If set to true, we will enforce usage of IPv4 or IPv6 addresses upon DNS 
        /// resolution for host names. If true, we will use IPv6 enforcement. Otherwise, 
        /// we will use IPv4.
        /// </summary>
        public readonly bool EnforceIpFamily;

        /// <summary>
        /// Sets the size of the connection backlog.
        /// </summary>
        public readonly int Backlog;

        /// <summary>
        /// Sets the default receive buffer size of the Sockets.
        /// </summary>
        public readonly int? ReceiveBufferSize;

        /// <summary>
        /// Sets the default send buffer size of the Sockets.
        /// </summary>
        public readonly int? SendBufferSize;
        public readonly int? WriteBufferHighWaterMark;
        public readonly int? WriteBufferLowWaterMark;

        /// <summary>
        /// Enables backwards compatibility with Akka.Remote clients running Helios 1.*
        /// </summary>
        public readonly bool BackwardsCompatibilityModeEnabled;

        /// <summary>
        /// When set to true, it will enable logging of DotNetty user events 
        /// and message frames.
        /// </summary>
        public readonly bool LogTransport;

        /// <summary>
        /// Byte order used by DotNetty, either big or little endian.
        /// By default a little endian is used to achieve compatibility with Helios.
        /// </summary>
        public readonly ByteOrder ByteOrder;

        /// <summary>
        /// Used mostly as a work-around for https://github.com/akkadotnet/akka.net/issues/3370
        /// on .NET Core on Linux. Should always be left to <c>true</c> unless running DotNetty v0.4.6
        /// on Linux, which can accidentally release buffers early and corrupt frames. Turn this setting
        /// to <c>false</c> to disable pooling and work-around this issue at the cost of some performance.
        /// </summary>
        public readonly bool EnableBufferPooling;

        /// <summary>
        /// When <c>true</c>, turns on reporting of the <see cref="PoolAllocatorDumpHandler"/>.
        /// </summary>
        public readonly bool EnableBufferPoolDumps;

        /// <summary>
        /// The sample rate at which we will be sampling the buffer pool dumps.
        /// </summary>
        public readonly double BufferPoolDumpSampleRate;

        /// <summary>
        /// When <c>true</c>, captures DotNetty logs into the Akka.NET <see cref="ILoggingAdapter"/>.
        /// </summary>
        public readonly bool CaptureDotNettyLogs;

        /// <summary>
        /// Determines how aggressively the <see cref="ResourceLeakDetector"/> should work
        /// to attempt to track DotNetty resource leaks in Akka.Remote.
        /// </summary>
        public readonly ResourceLeakDetector.DetectionLevel ResourceLeakDetectionLevel;

        public DiagnosticDotNettyTransportSettings(TransportMode transportMode, bool enableSsl, TimeSpan connectTimeout, string hostname, string publicHostname,
            int port, int? publicPort, int serverSocketWorkerPoolSize, int clientSocketWorkerPoolSize, int maxFrameSize, SslSettings ssl,
            bool dnsUseIpv6, bool tcpReuseAddr, bool tcpKeepAlive, bool tcpNoDelay, int backlog, bool enforceIpFamily,
            int? receiveBufferSize, int? sendBufferSize, int? writeBufferHighWaterMark, int? writeBufferLowWaterMark, bool backwardsCompatibilityModeEnabled, bool logTransport, ByteOrder byteOrder, bool enableBufferPooling, ResourceLeakDetector.DetectionLevel resourceLeakDetectionLevel, bool enableBufferPoolDumps, double bufferPoolDumpSampleRate, bool captureDotNettyLogs)
        {
            if (maxFrameSize < 32000) throw new ArgumentException("maximum-frame-size must be at least 32000 bytes", nameof(maxFrameSize));

            TransportMode = transportMode;
            EnableSsl = enableSsl;
            ConnectTimeout = connectTimeout;
            Hostname = hostname;
            PublicHostname = publicHostname;
            Port = port;
            PublicPort = publicPort;
            ServerSocketWorkerPoolSize = serverSocketWorkerPoolSize;
            ClientSocketWorkerPoolSize = clientSocketWorkerPoolSize;
            MaxFrameSize = maxFrameSize;
            Ssl = ssl;
            DnsUseIpv6 = dnsUseIpv6;
            TcpReuseAddr = tcpReuseAddr;
            TcpKeepAlive = tcpKeepAlive;
            TcpNoDelay = tcpNoDelay;
            Backlog = backlog;
            EnforceIpFamily = enforceIpFamily;
            ReceiveBufferSize = receiveBufferSize;
            SendBufferSize = sendBufferSize;
            WriteBufferHighWaterMark = writeBufferHighWaterMark;
            WriteBufferLowWaterMark = writeBufferLowWaterMark;
            BackwardsCompatibilityModeEnabled = backwardsCompatibilityModeEnabled;
            LogTransport = logTransport;
            ByteOrder = byteOrder;
            EnableBufferPooling = enableBufferPooling;
            ResourceLeakDetectionLevel = resourceLeakDetectionLevel;
            EnableBufferPoolDumps = enableBufferPoolDumps;
            BufferPoolDumpSampleRate = bufferPoolDumpSampleRate;
            CaptureDotNettyLogs = captureDotNettyLogs;
        }
    }
    internal enum TransportMode
    {
        Tcp,
        Udp
    }

    internal sealed class SslSettings
    {
        public static readonly SslSettings Empty = new SslSettings();
        public static SslSettings Create(Config config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config), "DotNetty SSL HOCON config was not found (default path: `akka.remote.dot-netty.Ssl`)");

            var flagsRaw = config.GetStringList("certificate.flags");
            var flags = flagsRaw.Aggregate(X509KeyStorageFlags.DefaultKeySet, (flag, str) => flag | ParseKeyStorageFlag(str));

            return new SslSettings(
                certificatePath: config.GetString("certificate.path"),
                certificatePassword: config.GetString("certificate.password"),
                flags: flags,
                suppressValidation: config.GetBoolean("suppress-validation", false));
        }

        private static X509KeyStorageFlags ParseKeyStorageFlag(string str)
        {
            switch (str)
            {
                case "default-key-set": return X509KeyStorageFlags.DefaultKeySet;
                case "exportable": return X509KeyStorageFlags.Exportable;
                case "machine-key-set": return X509KeyStorageFlags.MachineKeySet;
                case "persist-key-set": return X509KeyStorageFlags.PersistKeySet;
                case "user-key-set": return X509KeyStorageFlags.UserKeySet;
                case "user-protected": return X509KeyStorageFlags.UserProtected;
                default: throw new ArgumentException($"Unrecognized flag in X509 certificate config [{str}]. Available flags: default-key-set | exportable | machine-key-set | persist-key-set | user-key-set | user-protected");
            }
        }

        /// <summary>
        /// X509 certificate used to establish Secure Socket Layer (SSL) between two remote endpoints.
        /// </summary>
        public readonly X509Certificate2 Certificate;

        /// <summary>
        /// Flag used to suppress certificate validation - use true only, when on dev machine or for testing.
        /// </summary>
        public readonly bool SuppressValidation;

        public SslSettings()
        {
            Certificate = null;
            SuppressValidation = false;
        }

        public SslSettings(string certificatePath, string certificatePassword, X509KeyStorageFlags flags, bool suppressValidation)
        {
            if (string.IsNullOrEmpty(certificatePath))
                throw new ArgumentNullException(nameof(certificatePath), "Path to SSL certificate was not found (by default it can be found under `akka.remote.dot-netty.tcp.ssl.certificate.path`)");

            Certificate = new X509Certificate2(certificatePath, certificatePassword, flags);
            SuppressValidation = suppressValidation;
        }
    }
}
