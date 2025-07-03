using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using Azure.Core;
using Azure.Identity;
using Confluent.Kafka;

namespace KafkaTestC
{
    class Program
    {
        // Hard-coded configuration values - UPDATE THESE WITH YOUR ACTUAL VALUES
        private static string BootstrapServers = "pkc-w77k7w.centralus.azure.confluent.cloud:9092";
        private static string Topic = "test-topic";
        private static string Scope = "51ba109f-c8e0-4a62-96dd-64ad6abc1453";
        
        // These values must match your actual Confluent Cloud cluster details
        // The "lkc-abc123" format is just a placeholder - use your real cluster ID
        private static string LogicalClusterId = "lkc-135q06";
        
        // This should be your actual identity pool ID from Confluent Cloud
        private static string IdentityPoolId = "pool-jd8zv";

        static async Task Main(string[] args)
        {
            // Set up the librdkafka library path based on the current platform
            SetupLibrdkafkaPath();
            
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

        /// <summary>
        /// Sets up the librdkafka library path based on the current platform
        /// </summary>
        private static void SetupLibrdkafkaPath()
        {
            try
            {
                string? librdkafkaPath = null;
                
                // Determine the platform and set the appropriate path
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Console.WriteLine("Running on Linux platform");
                    // Try several possible locations for the library
                    string[] possiblePaths = {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "librdkafka", "linux-x64"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "linux-x64", "native"),
                        AppDomain.CurrentDomain.BaseDirectory
                    };
                    
                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path) && File.Exists(Path.Combine(path, "librdkafka.so")))
                        {
                            librdkafkaPath = path;
                            Console.WriteLine($"Found librdkafka at: {librdkafkaPath}");
                            break;
                        }
                    }
                    
                    if (librdkafkaPath == null)
                    {
                        Console.WriteLine("Warning: Could not find librdkafka native library directory");
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine("Running on Windows platform");
                    // Windows will use the default path from the NuGet package
                }
                
                // Set the path if found
                if (!string.IsNullOrEmpty(librdkafkaPath))
                {
                    // Set the environment variable that librdkafka uses to find its native libraries
                    Environment.SetEnvironmentVariable("CONFLUENT_KAFKA_LIBRDKAFKA_PATH", librdkafkaPath);
                    Console.WriteLine($"Set CONFLUENT_KAFKA_LIBRDKAFKA_PATH to: {librdkafkaPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up librdkafka path: {ex.Message}");
            }
        }

        static async Task RunProducer()
        {
            Console.WriteLine("Running as Producer with Azure Managed Identity. Press Ctrl+C to exit.");
            Console.WriteLine($"Using topic: {Topic}");

            // Set to true to attempt auto-creation of the topic
            bool autoCreateTopic = false;

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
                SaslOauthbearerConfig = $"extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                // If the above format doesn't work, try the more complete format:
                // SaslOauthbearerConfig = $"clientId='ignored' clientSecret='ignored' extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",

                // Add debug settings to see more information
                Debug = "security,broker,protocol",
                // Set to true to attempt auto-creation of the topic
                AllowAutoCreateTopics = autoCreateTopic
            };
            

            try
            {
                Console.WriteLine("Building producer client...");
                using var producer = new ProducerBuilder<string, string>(config)
                    .SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshCallback)
                    .SetErrorHandler((_, error) => {
                        Console.WriteLine($"Producer error: {error.Reason}. Code: {error.Code}, IsFatal: {error.IsFatal}");
                    })
                    .Build();

                // Verify topic exists by sending a test message
                try
                {
                    Console.WriteLine($"Checking topic access for '{Topic}'...");
                    
                    // Create an admin client to check topics
                    using (var adminClient = new AdminClientBuilder(new AdminClientConfig(config)).Build())
                    {
                        try 
                        {
                            var metadata = adminClient.GetMetadata(Topic, TimeSpan.FromSeconds(10));
                            bool topicExists = metadata.Topics.Any(t => t.Topic == Topic);
                            
                            if (!topicExists)
                            {
                                Console.WriteLine($"WARNING: Topic '{Topic}' does not exist! Please create it in Confluent Cloud first.");
                                Console.WriteLine("You may also need to verify that your identity has the proper ACL permissions to access this topic.");
                                
                                if (autoCreateTopic)
                                {
                                    Console.WriteLine("Attempting to auto-create the topic (requires proper permissions)...");
                                }
                                else 
                                {
                                    Console.WriteLine("You can set AllowAutoCreateTopics = true to attempt auto-creation, but this requires proper permissions.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Topic '{Topic}' exists. Continuing...");
                                // Display topic partitions information
                                var topicInfo = metadata.Topics.First(t => t.Topic == Topic);
                                Console.WriteLine($"Topic '{Topic}' has {topicInfo.Partitions.Count} partition(s)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing topic metadata: {ex.Message}");
                            if (ex.Message.Contains("authorization"))
                            {
                                Console.WriteLine("This appears to be an authorization issue. Make sure your identity has DESCRIBE permission for this topic.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking topic existence: {ex.Message}");
                    Console.WriteLine("This could indicate permission issues. Check your ACLs in Confluent Cloud.");
                }

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
                        
                        // Provide more detailed error information based on error code
                        if (e.Error.Code == ErrorCode.TopicAuthorizationFailed)
                        {
                            Console.WriteLine("Authorization error: Your identity does not have permission to write to this topic.");
                            PrintACLPermissionInfo();
                        }
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
            Console.WriteLine("Running as Consumer with Azure Managed Identity. Press Ctrl+C to exit.");
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
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.OAuthBearer,
                SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
                SaslOauthbearerClientId = "ignored", // Not used but required for config
                SaslOauthbearerClientSecret = "ignored", // Not used but required for config
                SaslOauthbearerTokenEndpointUrl = $"http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource='{Scope}'",
                // Using SaslOauthbearerConfig property directly - equivalent to Java's sasl.jaas.config
                // In Java, the full string would be:
                // org.apache.kafka.common.security.oauthbearer.OAuthBearerLoginModule required clientId='ignored' clientSecret='ignored' extension_logicalCluster='lkc-abc123' extension_identityPoolId='pool-xyz456';
                SaslOauthbearerConfig = $"extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                // If the above format doesn't work, try the more complete format:
                // SaslOauthbearerConfig = $"clientId='ignored' clientSecret='ignored' extension_logicalCluster='{LogicalClusterId}' extension_identityPoolId='{IdentityPoolId}'",
                GroupId = "kafka-test-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                // Recommended settings for production environments
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,
                FetchMaxBytes = 52428800,
                MaxPartitionFetchBytes = 1048576,
                // Add debug settings to see more information
                Debug = "security,broker,protocol"
            };
            
            try
            {
                Console.WriteLine("Building consumer client...");
                using var consumer = new ConsumerBuilder<string, string>(config)
                    .SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshCallback)
                    .SetErrorHandler((_, error) => {
                        Console.WriteLine($"Consumer error: {error.Reason}. Code: {error.Code}, IsFatal: {error.IsFatal}");
                        
                        if (error.Code == ErrorCode.TopicAuthorizationFailed)
                        {
                            Console.WriteLine("Authorization error: Your identity does not have permission to read from this topic.");
                            PrintACLPermissionInfo();
                        }
                    })
                    .Build();
                
                // Verify topic exists by checking metadata
                try
                {
                    Console.WriteLine($"Checking topic access for '{Topic}'...");
                    
                    // Create an admin client to check topics
                    using (var adminClient = new AdminClientBuilder(new AdminClientConfig(config)).Build())
                    {
                        try 
                        {
                            var metadata = adminClient.GetMetadata(Topic, TimeSpan.FromSeconds(10));
                            bool topicExists = metadata.Topics.Any(t => t.Topic == Topic);
                            
                            if (!topicExists)
                            {
                                Console.WriteLine($"WARNING: Topic '{Topic}' does not exist! Please create it in Confluent Cloud first.");
                            }
                            else
                            {
                                Console.WriteLine($"Topic '{Topic}' exists. Continuing...");
                                // Display topic partitions information
                                var topicInfo = metadata.Topics.First(t => t.Topic == Topic);
                                Console.WriteLine($"Topic '{Topic}' has {topicInfo.Partitions.Count} partition(s)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing topic metadata: {ex.Message}");
                            if (ex.Message.Contains("authorization"))
                            {
                                Console.WriteLine("This appears to be an authorization issue. Make sure your identity has DESCRIBE permission for this topic.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking topic: {ex.Message}");
                }
                
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
                            
                            if (e.Error.Code == ErrorCode.TopicAuthorizationFailed)
                            {
                                Console.WriteLine("Authorization error: Your identity does not have permission to read from this topic.");
                                PrintACLPermissionInfo();
                                break;
                            }
                            
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

        // Configuration is now hard-coded in class properties
        
        /// <summary>
        /// OAuth Bearer token refresh callback used by Kafka client to authenticate with Confluent Cloud
        /// </summary>
        private static void OAuthBearerTokenRefreshCallback(IClient client, string config)
        {
            try
            {
                Console.WriteLine("Requesting new OAuth token using Azure Managed Identity");
                Console.WriteLine($"Using LogicalClusterId: {LogicalClusterId}");
                Console.WriteLine($"Using IdentityPoolId: {IdentityPoolId}");
                Console.WriteLine($"OAUTH Config received: {config}");

                // Use the DefaultAzureCredential which will try multiple authentication methods including ManagedIdentity
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    // Uncomment the following line if you want to use specific managed identity
                    // ManagedIdentityClientId = ClientId 
                });

                // Get token using Azure Identity - note this is using Task.Run to run async code in sync context
                var tokenRequestContext = new TokenRequestContext(new[] { Scope });
                Console.WriteLine($"Requesting token for scope: {Scope}");
                var accessTokenTask = credential.GetTokenAsync(tokenRequestContext);
                AccessToken accessTokenResponse = accessTokenTask.AsTask().Result;

                // Create token value and expiration for Kafka client
                var tokenValue = accessTokenResponse.Token;
                var expirationMs = DateTimeOffset.UtcNow.AddMinutes(55).ToUnixTimeMilliseconds(); // Set expiry to 5 mins before actual expiry

                Console.WriteLine("Token obtained successfully. Setting on Kafka client...");
                
                // Create extensions dictionary with the required parameters
                var extensions = new Dictionary<string, string>
                {
                    {"logicalCluster", LogicalClusterId},
                    {"identityPoolId", IdentityPoolId}
                };

                // Set the token in the Kafka client with extensions
                client.OAuthBearerSetToken(tokenValue, expirationMs, "bearer", extensions);

                Console.WriteLine($"Successfully set OAuth token with extensions. Expires at: {DateTimeOffset.FromUnixTimeMilliseconds(expirationMs)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obtaining OAuth token: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                client.OAuthBearerSetTokenFailure(ex.ToString());
            }
        }

        /// <summary>
        /// Displays information about how to verify and fix permissions issues
        /// </summary>
        private static void PrintACLPermissionInfo()
        {
            Console.WriteLine("======= ACL PERMISSION INFORMATION =======");
            Console.WriteLine("Your application is facing a permission issue with Confluent Cloud.");
            Console.WriteLine("To fix this issue:");
            Console.WriteLine("1. Log in to Confluent Cloud console");
            Console.WriteLine($"2. Navigate to your cluster ({LogicalClusterId})");
            Console.WriteLine("3. Go to 'Security' → 'Accounts & Access'");
            Console.WriteLine($"4. Find and select your identity pool ({IdentityPoolId})");
            Console.WriteLine("5. Verify the topic permissions are correct for your identity");
            Console.WriteLine("   - For producer: Needs WRITE permission on the topic");
            Console.WriteLine("   - For consumer: Needs READ permission on the topic");
            Console.WriteLine("   - Both need DESCRIBE permission on the topic");
            Console.WriteLine("6. Also check if your consumer group has proper permissions");
            Console.WriteLine("======================================");
        }
    }
}
