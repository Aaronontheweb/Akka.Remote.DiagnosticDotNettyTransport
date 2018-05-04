using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor.Dsl;
using Akka.Actor;
using Akka.Configuration;
using Akka.Remote.DiagnosticDotNettyTransport.Configuration;
using Akka.Util.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Remote.DiagnosticDotNettyTransport.Tests
{
    public class TransportLoadSpecs : TestKit.Xunit.TestKit
    {
        public static readonly Config TransportLoadConfig = @"
            akka.actor.provider = remote
            akka.remote.dot-netty.diagnostic.tcp.port = 0
            akka.remote.dot-netty.diagnostic.tcp.hostname = localhost
        ";

        public TransportLoadSpecs(ITestOutputHelper helper)
            : base(TransportLoadConfig.WithFallback(DiagnosticConfig.DefaultDiagnosticConfiguration), null,
                output: helper)
        {
            Sys2 = ActorSystem.Create(Sys.Name, Sys.Settings.Config);
            InitializeLogger(Sys2);
        }

        public ActorSystem Sys2 { get; }

        public Address SysAddress => Sys.AsInstanceOf<ExtendedActorSystem>().Provider.DefaultAddress;

        public Address Sys2Address => Sys2.AsInstanceOf<ExtendedActorSystem>().Provider.DefaultAddress;

        [Fact(DisplayName = "Should load the DiagnosticDotNettyTransport from HOCON")]
        public void ShouldFormAssociationWithDiagnosticTransport()
        {
            var probe = CreateTestProbe(Sys2);
            var actor = Sys2.ActorOf(act =>
            {
                act.ReceiveAny((o, context) => context.Sender.Tell(o));
            }, "act");
            
            // verify the actor has been instantiated locally before we 
            // attempt to hit it via ActorSelection
            actor.Tell("hit1", probe);
            probe.ExpectMsg("hit1");

            Within(TimeSpan.FromSeconds(10), () =>
            {
                Sys2.ActorSelection(new RootActorPath(Sys2Address) / "user" / "act").Tell("hit", TestActor);
                ExpectMsg("hit");
            });
            
        }

        protected override void AfterAll()
        {
            Shutdown(Sys2);
        }
    }
}
