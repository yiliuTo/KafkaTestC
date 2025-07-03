# Kafka Test Client in C# for Confluent Cloud on Azure

This is a C# console application that demonstrates how to interact with Confluent Cloud on Azure. The application supports two authentication methods:

1. **Azure Managed Identity** (preferred): Uses OAuth with Azure Managed Identity for seamless authentication
2. **API Key Authentication** (fallback): Uses Confluent Cloud API keys when OAuth is not available

The application can run as either a producer or a consumer.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 9.0 or later)
- A Confluent Cloud cluster on Azure
- One of the following authentication methods:
  - Azure subscription with managed identity configured
  - Confluent Cloud API key and secret

## Configuration

The application configuration is now hard-coded in the Program.cs file. The key settings are:

```csharp
// Hard-coded configuration values
private static string BootstrapServers = "pkc-w77k7w.centralus.azure.confluent.cloud:9092";
private static string Topic = "test-topic";
private static string Scope = "51ba109f-c8e0-4a62-96dd-64ad6abc1453";
private static string LogicalClusterId = "lkc-abc123";
private static string IdentityPoolId = "pool-xyz456";
```

Update these values in Program.cs to match your specific Confluent Cloud and Azure environment.
```

Where:
- `<YOUR_CONFLUENT_CLOUD_BOOTSTRAP_SERVERS>`: The bootstrap servers URL from your Confluent Cloud cluster
- `<YOUR_AZURE_CLIENT_ID>`: The client ID of your Azure managed identity
- `<YOUR_AZURE_TENANT_ID>`: Your Azure tenant ID
- `<YOUR_CONFLUENT_CLOUD_SCOPE>`: The scope required for Confluent Cloud OAuth authentication

## Building and Running the Application

### Local Development Environment

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

### Deploying to Azure VM

This project includes scripts to deploy the application to an Azure VM with managed identity:

1. Build the self-contained application for Linux:

```powershell
dotnet publish -c Release -r linux-x64 --self-contained
```

2. Zip the [publish](.\bin\Release\net9.0\linux-x64\publish) and copy it into your vm:

```powershell
scp -i C:\Users\yiliu6\.ssh\id_rsa_new  .\bin\Release\net9.0\linux-x64\publish.zip azureuser@48.217.64.247:/tmp/KafkaTestC
```

3. Unzip it in vm and add exec permission to the application assemble, then run it:

```bash
sudo mv /tmp/KafkaTestC/publish.zip /opt/KafkaTestC/
cd /opt/KafkaTestC
sudo unzip publish.zip
sudo chmod +x publish/KafkaTestC
./publish/KafkaTestC
```

## As a Producer

- Enter messages when prompted
- Type `exit` to quit

## As a Consumer

- The consumer will automatically start reading messages from the topic
- Press `Ctrl+C` to exit

## Testing the Application

1. Make sure your Confluent Cloud cluster on Azure is running and properly configured
2. Run two instances of the application:
   - One as a Producer (Option 1)
   - One as a Consumer (Option 2)
3. Send messages from the Producer and observe them being received by the Consumer

## Additional Resources

- [Confluent Kafka .NET Client Documentation](https://docs.confluent.io/clients-confluent-kafka-dotnet/current/overview.html)
- [Azure Managed Identity Documentation](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [Confluent Cloud on Azure Documentation](https://docs.confluent.io/cloud/current/azure/index.html)
- [OAuth Authentication for Kafka](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_oauth.html)
