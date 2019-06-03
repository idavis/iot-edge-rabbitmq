using Serilog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Queueing;
using Contracts;
using System.Threading.Tasks;

namespace Analyzer
{
    public class EventingQueuePrioritizer
    {
        public QueueChannel ConsumerChannel { get; }
        public QueuePublisher QueuePublisher { get; }

        public EventingQueuePrioritizer(QueueChannel consumerChannel, QueueChannel publisherChannel)
        {
            ConsumerChannel = consumerChannel;

            var queuePublisher = new QueuePublisher(publisherChannel);
            QueuePublisher = queuePublisher;

            var consumer = new AsyncEventingBasicConsumer(consumerChannel.Model);
            consumer.Received += _OnMessageReceived;
            consumerChannel.Model.BasicConsume(queue: consumerChannel.QueueName,
                                               autoAck: false,
                                               consumer: consumer);
        }

        private async Task _OnMessageReceived(object model, BasicDeliverEventArgs ea)
        {
            Log.Debug("Picked up message {DeliveryTag}.", ea.DeliveryTag);

            var message = MessageSerialization.CreateFrom(ea);

            var priority = _GetMessagePriority(message);

            QueuePublisher.EnqueueJsonMessage(message, priority: priority);

            ConsumerChannel.Model.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

            Log.Debug("Finished message {DeliveryTag}", ea.DeliveryTag);
            await Task.CompletedTask;
        }

        // In this sample the message body is a Guid. To simplify the
        // priority logic, take the first character, if it is a digit,
        // then set that as the message priority; otherwise, set the priority
        // to 0 (lowest).
        private int _GetMessagePriority(Message message)
        {
            var initial = message.Payload.Body[0];
            var value = (int)char.GetNumericValue(initial);
            if (value == -1)
            {
                return 0;
            }
            return value;
        }
    }
}
