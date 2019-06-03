using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Extensions.Configuration;
using Queueing;
using Serilog;

namespace Consumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            _InitLogging();
            var moduleClient = await _InitEdge();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var queueChannel = await InitializePriorityQueueChannel(configuration);
            var queueConsumer = new EventingQueueConsumer(queueChannel, moduleClient);

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token);

            queueChannel.Dispose();
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

        static async Task<ModuleClient> _InitEdge()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Log.Information("IoT Hub module client initialized.");
            return ioTHubModuleClient;
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
