using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serilog;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Contracts;
using System.Linq;
using System.Diagnostics;

namespace Queueing
{
    public static class MessageSerialization
    {
        public static Message CreateFrom(BasicGetResult result)
        {
            var message = _ParseMessage(result.Body, result.BasicProperties);
            return message;
        }

        public static Message CreateFrom(BasicDeliverEventArgs args)
        {
            var message = _ParseMessage(args.Body, args.BasicProperties);
            return message;
        }

        private static Message _ParseMessage(byte[] body, IBasicProperties properties)
        {
            if (string.IsNullOrWhiteSpace(properties.ContentType))
            {
                throw new InvalidDataException("Message missing content type.");
            }

            if (string.Equals(properties.ContentType, "application/json"))
            {
                var encodingName = properties.IsContentEncodingPresent() ? properties.ContentEncoding : "utf-8";
                var encoding = Encoding.GetEncoding(encodingName);
                var text = encoding.GetString(body);
                Message message = Message.Parser.ParseJson(text);
                return message;
            }
            else if (properties.ContentType.StartsWith("application/protobuf"))
            {
                Message message = Message.Parser.ParseFrom(body);
                return message;
            }
            else
            {
                throw new InvalidDataException("Unexpected content type.");
            }
        }

        public static Message CreateFrom(byte[] body)
        {
            var text = Encoding.UTF8.GetString(body);
            Message message = Message.Parser.ParseJson(text);

            return message;
        }
    }
}
