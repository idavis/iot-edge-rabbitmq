using Serilog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Queueing;
using System;
using System.Threading;
using Microsoft.Azure.Devices.Client;
using Google.Protobuf;
using System.Text;
using System.Threading.Tasks;

namespace Consumer
{
    public class EventingQueueConsumer
    {
        public QueueChannel Channel { get; }
        public ModuleClient ModuleClient { get; }

        public EventingQueueConsumer(QueueChannel consumerChannel, ModuleClient moduleClient)
        {
            Channel = consumerChannel;
            ModuleClient = moduleClient;
            var consumer = new AsyncEventingBasicConsumer(consumerChannel.Model);
            consumer.Received += _OnMessageReceived;
            consumerChannel.Model.BasicConsume(queue: consumerChannel.QueueName,
                                               autoAck: false,
                                               consumer: consumer);
        }

        private async Task _OnMessageReceived(object sender, BasicDeliverEventArgs ea)
        {
            Log.Debug("Picked up message (priority: {Priority}), tag: {DeliveryTag}.", ea.BasicProperties.Priority, ea.DeliveryTag);

            var message = MessageSerialization.CreateFrom(ea);
            Log.Information("{body}", message);

            var json = JsonFormatter.Default.Format(message);
            var body = Encoding.UTF8.GetBytes(json);

            var hubMessage = new Message(body);
            await ModuleClient.SendEventAsync(hubMessage);

            // Slow down for the console output to be understandable
            await Task.Delay(TimeSpan.FromMilliseconds(50));

            Channel.Model.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

            Log.Debug("Finished message {DeliveryTag}", ea.DeliveryTag);
        }
    }
}
