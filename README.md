# Kafka Test Client in C#

This is a C# console application that demonstrates how to interact with Apache Kafka using the Confluent.Kafka client library. The application is configured to connect to a local Kafka broker running in Docker and can run as either a producer or a consumer.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 9.0 or later)
- Docker and Docker Compose for running Kafka locally
- VS Code (optional, but recommended)

## Project Structure

- `Program.cs` - Main application code with both producer and consumer functionality
- `docker-compose.yml` - Configuration for local Kafka setup with Docker

## Configuration

The application is configured to connect to a local Kafka broker running on `localhost:9092`. The key settings are defined in Program.cs:

```csharp
// Configuration for local Kafka broker
private static string BootstrapServers = "localhost:9092";
private static string Topic = "test-topic";
```

You can modify these values in Program.cs if needed.

## Running Kafka locally with Docker

1. Start the Kafka and Zookeeper containers:

```bash
# If using VS Code, run the task "start-kafka"
# Otherwise, use:
docker-compose up -d
```

2. To stop the containers:

```bash
# If using VS Code, run the task "stop-kafka"
# Otherwise, use:
docker-compose down
```

## Building and Running the Application

### Using VS Code Tasks

1. Start Kafka: Run the `start-kafka` task
2. Build the application: Run the `build` task
3. Run the application: Run the `run` task

### Using Terminal Commands

1. Build the application:

```bash
dotnet build
```

2. Run the application:

```bash
dotnet run
```

3. Choose one of the following options:
   - `1` to run as a Producer
   - `2` to run as a Consumer

## As a Producer

- Enter messages when prompted
- Type `exit` to quit

## As a Consumer

- The consumer will automatically start reading messages from the topic
- Press `Ctrl+C` to exit

## Testing the Application

1. Make sure your local Kafka broker is running (check with `docker ps`)
2. Run two instances of the application:
   - One as a Producer (Option 1)
   - One as a Consumer (Option 2)
3. Send messages from the Producer and observe them being received by the Consumer

## Technical Details

- Uses Confluent.Kafka client library for Kafka interaction
- Supports both producer and consumer roles
- Works with a locally running Kafka instance (default: localhost:9092)
- Default topic: "test-topic"
- Automatically creates topics if they don't exist

## Additional Resources

- [Confluent Kafka .NET Client Documentation](https://docs.confluent.io/clients-confluent-kafka-dotnet/current/overview.html)
- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
