using Google.Protobuf;
using RabbitMQ.Client;
using System.Text;
using System.Threading;
using Contracts;
using System.Collections.Generic;

namespace Queueing
{
    public class QueuePublisher
    {
        public QueueChannel Channel { get; }

        public QueuePublisher(QueueChannel channel)
        {
            Channel = channel;
        }

        public void EnqueueJsonMessage(Message message, bool persistent = true, int priority = 0, IDictionary<string,object> headers = null)
        {
            var json = JsonFormatter.Default.Format(message);
            var body = Encoding.UTF8.GetBytes(json);
            IBasicProperties properties = _GetProperties(persistent, priority, headers);
            properties.ContentType = "application/json";
            properties.ContentEncoding = "utf-8";
            Channel.Model.BasicPublish(exchange: "", routingKey: Channel.QueueName, mandatory: true, basicProperties: properties, body: body);
        }

        public void EnqueueBinaryMessage(Message message, bool persistent = true, int priority = 0, IDictionary<string,object> headers = null)
        {
            var body = message.ToByteArray();
            IBasicProperties properties = _GetProperties(persistent, priority, headers);
            properties.ContentType = $"application/protobuf; proto={typeof(Message).FullName}";
            Channel.Model.BasicPublish(exchange: "", routingKey: Channel.QueueName, mandatory: true, basicProperties: properties, body: body);
        }

        private IBasicProperties _GetProperties(bool persistent, int priority, IDictionary<string, object> headers)
        {
            var properties = Channel.Model.CreateBasicProperties();
            if (persistent)
            {
                properties.Persistent = true;
                properties.DeliveryMode = 2;
            }
            properties.Priority = (byte)priority;
            properties.Headers = headers;
            return properties;
        }
    }
}