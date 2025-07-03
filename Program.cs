using Confluent.Kafka;

namespace KafkaTestC
{
    class Program
    {
        // Configuration for local Kafka broker
        private static string BootstrapServers = "localhost:9092";
        private static string Topic = "test-topic";
        
        static async Task Main(string[] args)
        {
            
            // Display configuration values
            Console.WriteLine($"Using Bootstrap Servers: {BootstrapServers}");
            Console.WriteLine($"Topic: {Topic}");
            
            Console.WriteLine("Kafka Test Client - Choose an option:");
            Console.WriteLine("1. Run as Producer");
            Console.WriteLine("2. Run as Consumer");
            Console.Write("Option: ");

            var option = Console.ReadLine();

            switch (option)
            {
                case "1":
                    await RunProducer();
                    break;
                case "2":
                    await RunConsumer();
                    break;
                default:
                    Console.WriteLine("Invalid option. Exiting...");
                    break;
            }
        }

        static async Task RunProducer()
        {
            Console.WriteLine("Running as Producer. Press Ctrl+C to exit.");
            Console.WriteLine($"Using topic: {Topic}");

            // Set to true to attempt auto-creation of the topic if needed
            bool autoCreateTopic = true;

            var config = new ProducerConfig
            {
                BootstrapServers = BootstrapServers,
                // Using PLAINTEXT protocol for local development
                SecurityProtocol = SecurityProtocol.Plaintext,
                // Add debug settings if needed
                // Debug = "broker,topic,msg",
                // Set to true to attempt auto-creation of the topic
                AllowAutoCreateTopics = autoCreateTopic
            };
            
            try
            {
                Console.WriteLine("Building producer client...");
                using var producer = new ProducerBuilder<string, string>(config)
                    .SetErrorHandler((_, error) => {
                        Console.WriteLine($"Producer error: {error.Reason}. Code: {error.Code}, IsFatal: {error.IsFatal}");
                    })
                    .Build();

                Console.WriteLine("Producer ready. Enter messages to send.");
                while (true)
                {
                    Console.Write("Enter message (or 'exit' to quit): ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                        break;

                    var message = new Message<string, string>
                    {
                        Key = DateTime.UtcNow.Ticks.ToString(),
                        Value = input
                    };

                    try
                    {
                        var deliveryResult = await producer.ProduceAsync(Topic, message);
                        Console.WriteLine($"Delivered message to: {deliveryResult.TopicPartitionOffset}");
                    }
                    catch (ProduceException<string, string> e)
                    {
                        Console.WriteLine($"Delivery failed: {e.Error.Reason} (Code: {e.Error.Code})");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                Console.WriteLine($"Stack trace: {e.StackTrace}");
            }
        }

        static async Task RunConsumer()
        {
            Console.WriteLine("Running as Consumer. Press Ctrl+C to exit.");
            Console.WriteLine($"Using topic: {Topic}");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var config = new ConsumerConfig
            {
                BootstrapServers = BootstrapServers,
                // Using PLAINTEXT protocol for local development
                SecurityProtocol = SecurityProtocol.Plaintext,
                GroupId = "kafka-test-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                // Recommended settings for general use
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,
                FetchMaxBytes = 52428800,
                MaxPartitionFetchBytes = 1048576,
            };
            
            try
            {
                Console.WriteLine("Building consumer client...");
                using var consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, error) => {
                        Console.WriteLine($"Consumer error: {error.Reason}. Code: {error.Code}, IsFatal: {error.IsFatal}");
                    })
                    .Build();
                               
                Console.WriteLine("Subscribing to topic...");
                try
                {
                    consumer.Subscribe(Topic);
                    Console.WriteLine("Successfully subscribed to topic. Waiting for messages...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error subscribing to topic: {ex.Message}");
                    throw;
                }

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(cts.Token);

                            Console.WriteLine($"Received: {consumeResult.Message.Value} [Key: {consumeResult.Message.Key}] from {consumeResult.TopicPartitionOffset}");
                        }
                        catch (ConsumeException e)
                        {
                            Console.WriteLine($"Error consuming message: {e.Error.Reason} (Code: {e.Error.Code})");
                            
                            // Add a small delay to prevent log flooding in case of continuous errors
                            await Task.Delay(1000, cts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal when Ctrl+C is pressed
                }
                finally
                {
                    Console.WriteLine("Closing consumer...");
                    consumer.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Consumer error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            await Task.CompletedTask;
        }
    }
}
