<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

# Kafka Test Client in C#

This project is a C# console application that demonstrates interaction with Apache Kafka using the Confluent.Kafka client library.

## Project Structure
- `Program.cs` - Main application code with both producer and consumer functionality
- `docker-compose.yml` - Configuration for local Kafka setup with Docker

## Technical Details
- Uses Confluent.Kafka client library for Kafka interaction
- Supports both producer and consumer roles
- Works with a locally running Kafka instance (default: localhost:9092)
- Default topic: "test-topic"

When generating code for this project, consider:
- Proper error handling and resource disposal for Kafka clients
- Asynchronous message handling best practices
- Thread safety when appropriate
