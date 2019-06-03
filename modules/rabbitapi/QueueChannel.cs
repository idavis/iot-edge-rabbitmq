using System;
using RabbitMQ.Client;

namespace Queueing
{
    public class QueueChannel : IDisposable
    {
        public IConnection Connection { get; set; }
        public IModel Model { get; set; }
        public string QueueName { get; set; }

        public void Dispose()
        {
            Connection?.Dispose();
            Connection = null;
            Model?.Dispose();
            Model = null;
        }
    }
}