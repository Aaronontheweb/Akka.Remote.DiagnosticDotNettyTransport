using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Util;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace Akka.Remote.DiagnosticDotNettyTransport.Handlers
{
    /// <summary>
    /// INTERNAL API.
    /// 
    /// Used to record stat dumps by the <see cref="PooledByteBufferAllocator"/> in order to detect memory leaks.
    /// </summary>
    internal class PoolAllocatorDumpHandler : ChannelHandlerAdapter
    {
        /// <summary>
        ///     The maximum sample rate of 100%.
        /// </summary>
        public const double MaxSampleRate = 1.0d;

        /// <summary>
        ///     The default sample rate
        /// </summary>
        public const double DefaultSampleRate = 1.0d;

        /// <summary>
        ///     A sample rate of 0.
        /// </summary>
        public const double ZeroSampleRate = 0.0d;

        private readonly ILoggingAdapter _loggingAdapter;
        private readonly double _sampleRate;

        public PoolAllocatorDumpHandler(ILoggingAdapter loggingAdapter, double sampleRate)
        {
            _loggingAdapter = loggingAdapter;
            _sampleRate = sampleRate;
        }

        internal static bool IncludeInSample(double sampleRate)
        {
            return sampleRate == MaxSampleRate
                   || (sampleRate > ZeroSampleRate
                   && sampleRate >= ThreadLocalRandom.Current.NextDouble());
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (context.Allocator is PooledByteBufferAllocator pooled && IncludeInSample(_sampleRate))
            {
                _loggingAdapter.Info($"[Channel:{0}][READ] BufferStats:{1}", context.Channel.Id.AsShortText(), pooled.DumpStats());
            }
            base.ChannelRead(context, message);
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (context.Allocator is PooledByteBufferAllocator pooled && IncludeInSample(_sampleRate))
            {
                _loggingAdapter.Info($"[Channel:{0}][WRITE] BufferStats:{1}", context.Channel.Id.AsShortText(), pooled.DumpStats());
            }
            return base.WriteAsync(context, message);
        }

        public override void Flush(IChannelHandlerContext context)
        {
            if (context.Allocator is PooledByteBufferAllocator pooled && IncludeInSample(_sampleRate))
            {
                _loggingAdapter.Info($"[Channel:{0}][FLUSH] BufferStats:{1}", context.Channel.Id.AsShortText(), pooled.DumpStats());
            }
            base.Flush(context);
        }
    }
}
