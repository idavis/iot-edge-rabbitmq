using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Serilog;
using Queueing;
using Contracts;
using Microsoft.Extensions.Configuration;

namespace Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            _InitLogging();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            var _ = Task.Run(() => _GenerateMessages(configuration), cts.Token);

            await WhenCancelled(cts.Token);
        }

        private static async Task _GenerateMessages(IConfiguration configuration)
        {
            var queueChannel = await _InitializeWorkQueueChannel(configuration);
            var queuePublisher = new QueuePublisher(queueChannel);

            var iterationDelay = configuration.GetValue("IterationDelay", TimeSpan.FromSeconds(30));
            var batchSize = configuration.GetValue("BatchSize", 96);
            var remainingMessagesToSend = configuration.GetValue("RemainingMessagesToSend", 500);

            while (remainingMessagesToSend > 0)
            {
                for (int i = 0; i < batchSize && remainingMessagesToSend > 0; remainingMessagesToSend--, i++)
                {
                    _SendMessage(queuePublisher);
                }
                
                Log.Debug("Sent message batch");
                
                if (remainingMessagesToSend > 0)
                {
                    await Task.Delay(iterationDelay);
                }
            }

            queueChannel.Dispose();
        }

        private static void _SendMessage(QueuePublisher queuePublisher)
        {
            var message = new Message
            {
                Header = new Header
                {
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                },
                Payload = new Payload
                {
                    Body = Guid.NewGuid().ToString(),
                }
            };

            queuePublisher.EnqueueJsonMessage(message);
            message.Payload.Body = Guid.NewGuid().ToString();

            queuePublisher.EnqueueBinaryMessage(message);
        }

        private static void _InitLogging()
        {
            var log = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#endif
                .WriteTo.Console()
                .CreateLogger();
            Log.Logger = log;
        }

        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task<QueueChannel> _InitializeWorkQueueChannel(IConfiguration configuration)
        {
            var queueName = configuration.GetValue("WorkQueueName", "work_queue");
            var queueChannelFactory = new QueueChannelFactory(queueName, configuration);
            var queueChannel = await queueChannelFactory.CreateQueue();
            return queueChannel;
        }
    }
}
