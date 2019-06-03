using Serilog;
using System;
using RabbitMQ.Client;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Queueing
{
    public class QueueChannelFactory
    {
        const string RABBITMQ_DEFAULT_USER = "RABBITMQ_DEFAULT_USER";
        const string RABBITMQ_DEFAULT_PASS = "RABBITMQ_DEFAULT_PASS";
        const string RABBITMQ_HOSTNAME = "RABBITMQ_HOSTNAME";
        const string RABBITMQ_VIRTUAL_HOST = "RABBITMQ_VIRTUAL_HOST";

        public QueueChannelFactory(string queueName, IConfiguration configuration)
        {
            QueueName = queueName;
            Configuration = configuration;
            ConnectionRetryDelay = configuration.GetValue("ConnectionRetryDelay", TimeSpan.FromSeconds(5));
        }

        public string QueueName { get; }
        public IConfiguration Configuration { get; }
        public TimeSpan ConnectionRetryDelay { get; }

        public async Task<QueueChannel> CreateQueue(bool priority = false)
        {
            var channel = await _CreateQueue(QueueName, priority);
            return channel;
        }

        private async Task<QueueChannel> _CreateQueue(string queueName, bool priority)
        {
            var connection = await _CreateMQConnection();
            var model = _CreateChannel(connection, queueName, priority);
            var taskQueueChannel = new QueueChannel
            {
                Connection = connection,
                Model = model,
                QueueName = queueName,
            };
            Log.Debug("Created channel factory for {queue}", queueName);
            return taskQueueChannel;
        }

        private IModel _CreateChannel(IConnection connection, string queueName, bool priority)
        {
            var channel = connection.CreateModel();

            if (priority)
            {
                var args = new Dictionary<string, object>();
                args.Add("x-max-priority", 10);
                var declareResult = channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, args);
                Log.Information("Created {QueueName} with {DeclareResult} and priority.", queueName, declareResult);
            }
            else
            {
                var declareResult = channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                Log.Information("Created {QueueName} with {DeclareResult}", queueName, declareResult);
            }

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            return channel;
        }

        private async Task<IConnection> _CreateMQConnection()
        {
            var factory = _CreateConnectionFactoryFromEnvironment();
            while (true)
            {
                try
                {
                    var connection = factory.CreateConnection();
                    return connection;
                }
                catch (Exception ex)
                {
                    await Task.Delay(ConnectionRetryDelay);
                    Log.Warning(ex.ToString());
                }
            }
        }

        private ConnectionFactory _CreateConnectionFactoryFromEnvironment()
        {
            string userName = Configuration.GetValue(RABBITMQ_DEFAULT_USER, "");
            string password = Configuration.GetValue(RABBITMQ_DEFAULT_PASS, "");
            string hostName = Configuration.GetValue(RABBITMQ_HOSTNAME, "");
            string virtualHost = Configuration.GetValue(RABBITMQ_VIRTUAL_HOST, "/");

            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException($"{RABBITMQ_DEFAULT_USER} must be defined.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"{RABBITMQ_DEFAULT_PASS} must be defined.");
            }

            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new InvalidOperationException($"{RABBITMQ_HOSTNAME} must be defined.");
            }

            var factory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                UserName = userName,
                Password = password,
                VirtualHost = virtualHost,
                HostName = hostName,
                DispatchConsumersAsync = true,
            };
            return factory;
        }

        private string _GetValueFromEnvironment(IDictionary envVariables, string variableName)
        {
            if (envVariables.Contains(variableName))
            {
                return envVariables[variableName].ToString();
            }

            return null;
        }
    }
}