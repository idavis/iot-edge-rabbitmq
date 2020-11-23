# Azure IoT Edge & RabbitMQ

The Azure IoT Edge EdgeHub Module provides a persistent queue for inter-Module messaging and Edge to IoT Hub messaging. Starting in 1.0.10, the EdgeHub supports prioritized delivery via [declared route endpoint priorities](https://docs.microsoft.com/en-us/azure/iot-edge/module-composition?view=iotedge-2018-06#declare-routes). This update still doesn't allow for other messaging scenarios where packages like RabbitMQ shine.

For these other scenarios (as well as message prioritization), RabbitMQ can be used in Azure IoT Edge deployments. RabbitMQ may also be a good choice due to its other options.  For example, RabbitMQ supports both transient (in-memory) queues as well as persistent queues. As shown in this sample, persistent queues can be stored on the Edge host machine, and accessed individually by Edge modules.  

By itself RabbitMQ can only provide priority messaging between Edge modules. Azure IoT Hub also does not support priority queuing.  However, by combining RabbitMQ in an EdgeModule with a separate DeviceClient connection in the module, it is possible to implement a single high-priority message channel to IoT Hub, that bypasses the ModuleClient queue.

It should be noted that priority queuing doesn't work for transparent gateways as modules aren't deployed to do the queue reading/publishing/prioritizing. Any devices or messages will be queued in the back.

This sample application shows how to implement RabbitMQ queues inside of Azure IoT Edge deployments with regular and priority queues.  In addition, it shows how to leverage probuf contracts to send/receive strongly typed messages serialized as json or binary.

# Components

## Producer

The producer module generates regular (no priority) binary and json messages and pushes them to a persistent rabbitmq queue. It pauses periodically (based on configuration) to to simulate bursts of work or pauses in telemetry data.

## Analyzer

The analyzer module reads the messages from work queue and pushes the messages into a prioritized work queue.  For this sample, the priority is randomly generated based on message Guid's first digit.

## Consumer

The consumer module reads the messages from the priority queue, highest to lowest, and writes them to the screen, and forwards them to IoT Hub (via the EdgeHub queue using ModuleClient). It sleeps long enough for the output to be seen in the output window.  As noted above, an Edge module could instantiate a DeviceClient in order to send high priority messages to IoT Hub out-of-band from the EdgeHub queue.

Multiple copies of this module can be spawned to implement multi-reader queues, but the upstream connection to IoT Hub is still FIFO. To add a consumer, create a copy of the consumer module deployment configuration such as:

```json
"secondConsumer": {
  "version": "1.0",
  "type": "docker",
  "status": "running",
  "restartPolicy": "always",
  "settings": {
    "image": "${MODULES.consumer}",
    "createOptions": {
      "Env": [
        "RABBITMQ_DEFAULT_USER=$RABBITMQ_DEFAULT_USER",
        "RABBITMQ_DEFAULT_PASS=$RABBITMQ_DEFAULT_PASS",
        "RABBITMQ_HOSTNAME=$RABBITMQ_HOSTNAME"
      ]
    }
  }
}
```

The add a route to the deployment:

```json
"secondConsumerToIoTHub": "FROM /messages/modules/secondConsumer/outputs/* INTO $upstream"
```

## RabbitMQ

In order to set up a multi-reader persistent/durable queue, [RabbitMQ](https://www.rabbitmq.com/) has been configured in an Azure IoT Edge deployment. Detailed configuration of the module is [covered below](rabitmq-with-containers).

### RabitMQ with Containers

https://hub.docker.com/_/rabbitmq

In order to run the sample, several environment variables are required.  For developmment, these can be set in the .env file.  There is a sample .env.temp provided.

Be sure to follow the [production checklist](http://www.rabbitmq.com/production-checklist.html) when deploying beyond dev.

#### Built-in RabbbitMQ environment variables

For setting up the connection user credentials. This defaults to guest/guest if not set, but turns off remote connections.

- `RABBITMQ_DEFAULT_USER`
- `RABBITMQ_DEFAULT_PASS`

If transport encryption is required, SSL configuration, without the management plugin,can be configured throught he following variables:

- `RABBITMQ_SSL_CACERTFILE`
- `RABBITMQ_SSL_CERTFILE`
- `RABBITMQ_SSL_DEPTH`
- `RABBITMQ_SSL_FAIL_IF_NO_PEER_CERT`
- `RABBITMQ_SSL_KEYFILE`
- `RABBITMQ_SSL_VERIFY`

See the [RabbitMQ documentation on SSL](http://www.rabbitmq.com/ssl.html) for more information.

#### Custom environment variables for configuring on edge

##### RABBITMQ_DATA_DIR

Where to store our persistent/durable queue messages.  This needs to be a path on the container host OS and must exist prior to running the module.  For example, the following could used on Linux:

`RABBITMQ_DATA_DIR=/mnt/data/rabbit`

On Windows, the following could be used:

`RABBITMQ_DATA_DIR="C:\\ProgramData\\rabbit"`

Name for the host. RabbitMQ creates a queue based on the host name. If this isn't set or is changed, the saved messages will no longer be available.

##### RABBITMQ_CONTAINER_HOST

`RABBITMQ_CONTAINER_HOST=roger`

This needs to match the deployment manifest module name used to host RabbitMQ. The queue consumers use it to find the endpoint.

##### RABBITMQ_HOSTNAME

`RABBITMQ_HOSTNAME=rabbit`

##### Container Registry

Edge modules require a container registry.  If you choose to build locally or use a local registry container (ie: `"localhost:5000"`), username and password are not required.

- `CONTAINER_REGISTRY_SERVER`
- `CONTAINER_REGISTRY_USERNAME`
- `CONTAINER_REGISTRY_PASSWORD`
