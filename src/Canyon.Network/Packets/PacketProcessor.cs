using Canyon.Shared;
using Canyon.Network.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Canyon.Network.Packets
{
    /// <summary>
    ///     Packet processor for handling packets in background tasks using unbounded
    ///     channel. Allows for multiple writers, such as each remote client's accepted socket
    ///     receive loop, to write to an assigned channel. Each reader has an associated
    ///     channel to guarantee client packet processing order.
    /// </summary>
    /// <typeparam name="TClient">Type of client being processed with the packet</typeparam>
    public class PacketProcessor<TClient> : BackgroundService
        where TClient : TcpServerActor
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<PacketProcessor<TClient>>();

        // Fields and Properties
        protected readonly Task[] ReadBackgroundTasks;
        protected readonly Channel<Message>[] ReadChannels;
        protected readonly Task[] WriteBackgroundTasks;
        protected readonly Channel<Message>[] WriteChannels;
        protected readonly Partition[] Partitions;
        protected readonly Func<TClient, byte[], Task> Process;
        private readonly CancellationTokenSource cancelReadsSource = new();
        private readonly CancellationTokenSource cancelWritesSource = new();
        protected CancellationToken CancelReads => cancelReadsSource.Token;
        protected CancellationToken CancelWrites => cancelWritesSource.Token;

        /// <summary>
        ///     Instantiates a new instance of <see cref="PacketProcessor{TClient}" /> using a default
        ///     amount of worker tasks to initialize. Tasks will not be started.
        /// </summary>
        /// <param name="process">Processing task for channel messages</param>
        /// <param name="count">Number of threads to be created</param>
        public PacketProcessor(
            Func<TClient, byte[], Task> process,
            int count = 0)
        {
            // Initialize the channels and tasks as parallel arrays
            count = count == 0 ? Math.Max(1, Environment.ProcessorCount / 2) : count;
            ReadBackgroundTasks = new Task[count];
            ReadChannels = new Channel<Message>[count];
            WriteBackgroundTasks = new Task[count];
            WriteChannels = new Channel<Message>[count];
            Partitions = new Partition[count];
            Process = process;
        }

        /// <summary>
        ///     Triggered when the application host is ready to execute background tasks for
        ///     dequeuing and processing work from unbounded channels. Work is queued by a
        ///     connected and assigned client.
        /// </summary>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            for (var i = 0; i < ReadBackgroundTasks.Length; i++)
            {
                Partitions[i] = new Partition { ID = (uint)i };
                ReadChannels[i] = Channel.CreateUnbounded<Message>();
                ReadBackgroundTasks[i] = DequeueReadAsync(ReadChannels[i]);
            }

            for (var i = 0; i < WriteBackgroundTasks.Length; i++)
            {
                WriteChannels[i] = Channel.CreateUnbounded<Message>();
                WriteBackgroundTasks[i] = DequeueWriteAsync(WriteChannels[i]);
            }

            return Task.WhenAll(ReadBackgroundTasks);
        }

        /// <summary>
        ///     Queues work by writing to a message channel. Work is queued by a connected
        ///     client, and dequeued by the server's packet processing worker tasks. Each
        ///     work item contains a single packet to be processed.
        /// </summary>
        /// <param name="actor">Actor requesting packet processing</param>
        /// <param name="packet">Packet bytes to be processed</param>
        public void QueueRead(TClient actor, byte[] packet)
        {
            if (!CancelWrites.IsCancellationRequested)
            {
                ReadChannels[actor.Partition].Writer.TryWrite(new Message
                {
                    Actor = actor,
                    Packet = packet
                });
            }
        }

        public void QueueWrite(TClient actor, byte[] packet)
        {
            if (!CancelWrites.IsCancellationRequested)
            {
                WriteChannels[actor.Partition].Writer.TryWrite(new Message
                {
                    Actor = actor,
                    Packet = packet
                });
            }
        }

        public void QueueWrite(TClient actor, byte[] packet, Func<Task> task)
        {
            if (!CancelWrites.IsCancellationRequested)
            {
                WriteChannels[actor.Partition].Writer.TryWrite(new Message
                {
                    Actor = actor,
                    Packet = packet,
                    Action = task
                });
            }
        }

        /// <summary>Queue a packet and report whether all bytes were sent to the local socket.</summary>
        public void QueueWriteWithResult(TClient actor, byte[] packet, Action<bool> completion)
        {
            if (CancelWrites.IsCancellationRequested ||
                !WriteChannels[actor.Partition].Writer.TryWrite(new Message
                {
                    Actor = actor,
                    Packet = packet,
                    Completion = completion
                }))
            {
                completion?.Invoke(false);
            }
        }

        /// <summary>
        ///     Dequeues work in a loop. For as long as the thread is running and work is
        ///     available, work will be dequeued and processed. After dequeuing a message,
        ///     the packet processor's <see cref="Process" /> action will be called.
        /// </summary>
        /// <param name="channel">Channel to read messages from</param>
        protected async Task DequeueReadAsync(Channel<Message> channel)
        {
            while (!CancelReads.IsCancellationRequested)
            {
                Message msg;
                try
                {
                    msg = await channel.Reader.ReadAsync(CancelReads);
                }
                catch (OperationCanceledException) when (CancelReads.IsCancellationRequested)
                {
                    break;
                }

                if (msg != null)
                {
                    try
                    {
                        await Process(msg.Actor, msg.Packet).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled exception processing incoming packet for [{IpAddress}]: {Message}", msg.Actor?.IpAddress, ex.Message);
                    }
                }
            }
        }

        protected async Task DequeueWriteAsync(Channel<Message> channel)
        {
            while (!CancelReads.IsCancellationRequested)
            {
                Message msg;
                try
                {
                    msg = await channel.Reader.ReadAsync(CancelReads);
                }
                catch (OperationCanceledException) when (CancelReads.IsCancellationRequested)
                {
                    break;
                }

                if (msg != null)
                {
                    try
                    {
                        bool sent = await msg.Actor.InternalSendAsync(msg.Packet).ConfigureAwait(false);
                        msg.Completion?.Invoke(sent);
                        if (msg.Action != null)
                        {
                            await msg.Action().ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        msg.Completion?.Invoke(false);
                        logger.LogError(ex, "Unhandled exception sending outgoing packet to [{IpAddress}]: {Message}", msg.Actor?.IpAddress, ex.Message);
                    }
                }
            }
        }

        /// <summary>
        ///     Triggered when the application host is stopping the background task with a
        ///     graceful shutdown. Requests that writes into the channel stop, and then reads
        ///     from the channel stop.
        /// </summary>
        public new async Task StopAsync(CancellationToken cancellationToken)
        {
            cancelWritesSource.Cancel();
            cancelReadsSource.Cancel();
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        ///     Selects a partition for the client actor based on partition weight. The
        ///     partition with the least population will be chosen first. After selecting a
        ///     partition, that partition's weight will be increased by one.
        /// </summary>
        public uint SelectPartition()
        {
            uint partition = Partitions.Aggregate((aggr, next) =>
                                                      next.Weight.CompareTo(aggr.Weight) < 0 ? next : aggr).ID;
            Interlocked.Increment(ref Partitions[partition].Weight);
            return partition;
        }

        /// <summary>
        ///     Deselects a partition after the client actor disconnects.
        /// </summary>
        /// <param name="partition">The partition id to reduce the weight of</param>
        public void DeselectPartition(uint partition)
        {
            Interlocked.Decrement(ref Partitions[partition].Weight);
        }

        /// <summary>
        ///     Defines a message for the <see cref="PacketProcessor{TClient}" />'s unbounded channel
        ///     for queuing packets and actors requesting work. Each message defines a single
        ///     unit of work - a single packet for processing.
        /// </summary>
        protected class Message
        {
            public TClient Actor { get; set; }
            public byte[] Packet { get; set; }
            public Func<Task> Action { get; set; }
            public Action<bool> Completion { get; set; }
        }

        /// <summary>
        ///     Defines a partition for the <see cref="PacketProcessor{TClient}" />. This allows the
        ///     background service to track partition weight and assign clients to less
        ///     populated partitions.
        /// </summary>
        protected class Partition
        {
            public uint ID;
            public int Weight;
        }
    }
}
