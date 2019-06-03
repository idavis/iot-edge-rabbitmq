using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Queueing;
using Serilog;

namespace Analyzer
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

            var publisherChannel = await InitializePriorityQueueChannel(configuration);
            var consumerChannel = await InitializeWorkQueueChannel(configuration);
            var queueConsumer = new EventingQueuePrioritizer(consumerChannel, publisherChannel);

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token);

            consumerChannel.Dispose();
            publisherChannel.Dispose();
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

        static async Task<QueueChannel> InitializeWorkQueueChannel(IConfiguration configuration)
        {
            var queueName = configuration.GetValue("WorkQueueName", "work_queue");
            var queueChannelFactory = new QueueChannelFactory(queueName, configuration);
            var queueChannel = await queueChannelFactory.CreateQueue();
            return queueChannel;
        }

        static async Task<QueueChannel> InitializePriorityQueueChannel(IConfiguration configuration)
        {
            var queueName = configuration.GetValue("PrioritizedWorkQueueName", "prioritized_work_queue");
            var queueChannelFactory = new QueueChannelFactory(queueName, configuration);
            var queueChannel = await queueChannelFactory.CreateQueue(priority: true);
            return queueChannel;
        }
    }
}
