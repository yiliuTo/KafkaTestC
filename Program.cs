using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Core;
using Azure.Identity;
using Confluent.Kafka;

namespace KafkaTestC
{
    class Program
    {
        // Hard-coded configuration values
        private static string BootstrapServers = "pkc-w77k7w.centralus.azure.confluent.cloud:9092";
        private static string Topic = "test-topic";
        private static string Scope = "51ba109f-c8e0-4a62-96dd-64ad6abc1453";
        private static string LogicalClusterId = "lkc-abc123";
        private static string IdentityPoolId = "pool-xyz456";

        static async Task Main(string[] args)
        {
            // Display configuration values
            Console.WriteLine($"Using Bootstrap Servers: {BootstrapServers}");
            Console.WriteLine($"Topic: {Topic}");
            Console.WriteLine($"Logical Cluster ID: {LogicalClusterId}");
            Console.WriteLine($"Identity Pool ID: {IdentityPoolId}");
            
            Console.WriteLine("Confluent Cloud on Azure with Managed Identity - Choose an option:");
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
            Console.WriteLine("Running as Producer with Azure Managed Identity. Press Ctrl+C to exit.");

            var config = new ProducerConfig
            {
                BootstrapServers = BootstrapServers,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.OAuthBearer,
                SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
                SaslOauthbearerClientId = "ignored", // Not used but required for config
                SaslOauthbearerClientSecret = "ignored", // Not used but required for config
                SaslOauthbearerTokenEndpointUrl = $"http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource='{Scope}'",
                // Using SaslOauthbearerConfig property directly - equivalent to Java's sasl.jaas.config
                // In Java, the full string would be:
                // org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required clientId='ignored' clientSecret='ignored' extension_logicalCluster='lkc-abc123' extension_identityPoolId='pool-xyz456';
                // SaslOauthbearerConfig = $"clientId='ignored' clientSecret='ignored' extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                SaslOauthbearerConfig = $"extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                Acks = Acks.All,
                // Recommended settings for production environments
                EnableIdempotence = true,
                MaxInFlight = 5,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000
            };
            

            using var producer = new ProducerBuilder<string, string>(config)
                .SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshCallback)
                .Build();

            try
            {
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

                    var deliveryResult = await producer.ProduceAsync(Topic, message);
                    
                    Console.WriteLine($"Delivered message to: {deliveryResult.TopicPartitionOffset}");
                }
            }
            catch (ProduceException<string, string> e)
            {
                Console.WriteLine($"Delivery failed: {e.Error.Reason}");
            }
        }

        static async Task RunConsumer()
        {
            Console.WriteLine("Running as Consumer with Azure Managed Identity. Press Ctrl+C to exit.");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var config = new ConsumerConfig
            {
                BootstrapServers = BootstrapServers,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.OAuthBearer,
                SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
                SaslOauthbearerClientId = "ignored", // Not used but required for config
                SaslOauthbearerClientSecret = "ignored", // Not used but required for config
                SaslOauthbearerTokenEndpointUrl = $"http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource='{Scope}'",
                // Using SaslOauthbearerConfig property directly - equivalent to Java's sasl.jaas.config
                // In Java, the full string would be:
                // org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required clientId='ignored' clientSecret='ignored' extension_logicalCluster='lkc-abc123' extension_identityPoolId='pool-xyz456';
                //SaslOauthbearerConfig = $"clientId='ignored' clientSecret='ignored' extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                SaslOauthbearerConfig = $"extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                GroupId = "kafka-test-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                // Recommended settings for production environments
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,
                FetchMaxBytes = 52428800,
                MaxPartitionFetchBytes = 1048576
            };
            

            using var consumer = new ConsumerBuilder<string, string>(config)
                .SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshCallback)
                .Build();
            consumer.Subscribe(Topic);

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
                        Console.WriteLine($"Error consuming message: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal when Ctrl+C is pressed
            }
            finally
            {
                consumer.Close();
            }

            await Task.CompletedTask;
        }

        // Configuration is now hard-coded in class properties
        
        /// <summary>
        /// OAuth Bearer token refresh callback used by Kafka client to authenticate with Confluent Cloud
        /// </summary>
        private static void OAuthBearerTokenRefreshCallback(IClient client, string config)
        {
            try
            {
                Console.WriteLine("Requesting new OAuth token using Azure Managed Identity");

                // Use the DefaultAzureCredential which will try multiple authentication methods including ManagedIdentity
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    // Uncomment the following line if you want to use specific managed identity
                    // ManagedIdentityClientId = ClientId 
                });

                // Get token using Azure Identity - note this is using Task.Run to run async code in sync context
                var tokenRequestContext = new TokenRequestContext(new[] { Scope });
                var accessTokenTask = credential.GetTokenAsync(tokenRequestContext);
                AccessToken accessTokenResponse = accessTokenTask.AsTask().Result;

                // Create token value and expiration for Kafka client
                var tokenValue = accessTokenResponse.Token;
                var expirationMs = DateTimeOffset.UtcNow.AddMinutes(55).ToUnixTimeMilliseconds(); // Set expiry to 5 mins before actual expiry

                // Set the token in the Kafka client - using the extension method properly
                client.OAuthBearerSetToken(tokenValue, expirationMs, "bearer", null);

                Console.WriteLine($"Successfully obtained OAuth token. Expires at: {DateTimeOffset.FromUnixTimeMilliseconds(expirationMs)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obtaining OAuth token: {ex.Message}");
                client.OAuthBearerSetTokenFailure(ex.ToString());
            }
        }
    }
}
