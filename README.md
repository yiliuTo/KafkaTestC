# Kafka Test Client in C# for Confluent Cloud on Azure

This is a C# console application that demonstrates how to interact with Confluent Cloud on Azure using Azure managed identity for authentication. The application can run as either a producer or a consumer.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 9.0 or later)
- A Confluent Cloud cluster on Azure
- Azure subscription with managed identity configured

## Setting Up with Confluent Cloud on Azure

1. Create a Confluent Cloud cluster on Azure following [Confluent's documentation](https://docs.confluent.io/cloud/current/azure/index.html).

2. Configure Azure managed identity for your application by following these steps:
   - Create a managed identity in Azure
   - Assign appropriate permissions to access your Confluent Cloud resources
   - Set up OAuth integration between Azure AD and Confluent Cloud

## Configuration

The application configuration is now hard-coded in the Program.cs file. The key settings are:

```csharp
// Hard-coded configuration values
private static string BootstrapServers = "pkc-w77k7w.centralus.azure.confluent.cloud:9092";
private static string Topic = "test-topic";
private static string ClientId = "your-client-id"; 
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

#### Windows VM Deployment

1. Build the self-contained application for Windows:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

2. Use the provided deployment scripts:

```powershell
# Deploy a new Windows VM with managed identity
.\Deploy-KafkaVM.ps1 -ResourceGroupName "your-resource-group" -Location "westus2"

# Deploy the application to the VM
.\Deploy-To-AzureVM.ps1 -VmName "kafka-client-vm" -ResourceGroup "your-resource-group"

# Or use the full deployment script
.\Deploy-Full-Solution.ps1 -ResourceGroupName "your-resource-group"
```

#### Linux VM Deployment

1. Build the self-contained application for Linux:

```powershell
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

2. Use the provided Linux deployment scripts:

```powershell
# Deploy a new Linux VM with managed identity
.\Deploy-KafkaLinuxVM.ps1 -ResourceGroupName "your-resource-group" -Location "westus2"

# Deploy the application to the VM
.\Deploy-To-LinuxVM.ps1 -VmName "kafka-client-linux-vm" -ResourceGroup "your-resource-group"

# Or use the full Linux deployment script
.\Deploy-Full-Linux-Solution.ps1 -ResourceGroupName "your-resource-group"
```

Refer to the `DEPLOYMENT.md` file for detailed deployment instructions.

## As a Producer

- Enter messages when prompted
- Type `exit` to quit

## As a Consumer

- The consumer will automatically start reading messages from the topic
- Press `Ctrl+C` to exit

## Additional Settings

You can modify the following settings in your `appsettings.json` file:

- `Topic`: Topic name (default: "test-topic")
- `GroupId`: Consumer group ID (default: "kafka-test-consumer-group")
- Other security and authentication settings as needed

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
