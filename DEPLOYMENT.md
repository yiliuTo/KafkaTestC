# Deploying Kafka Test Client to Azure VM

This document provides instructions for deploying the Kafka Test Client application to an Azure Virtual Machine (Windows or Linux).

## Prerequisites

1. An Azure subscription with permissions to create or access Virtual Machines
2. Windows Server or Windows 10/11 VM, or Linux VM (Ubuntu recommended) running on Azure
3. Proper network configuration to allow communication with Confluent Cloud (port 9092)
4. Azure Managed Identity configured on the VM with appropriate permissions

## Deployment Steps

### 1. Prepare the Deployment Package

#### For Windows VM
The application can be built as a self-contained executable for Windows with the following command:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true
```

#### For Linux VM
To deploy to a Linux VM, build the application as a self-contained executable for Linux:

```powershell
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### 2. Deploy to the Azure VM

#### Option 1: Manual Deployment - Windows VM

1. Connect to your Azure Windows VM using RDP or Azure Bastion
2. Create a folder for the application, e.g., `C:\KafkaTestC`
3. Copy the `KafkaTestC.exe` file from the `bin\Release\net9.0\win-x64\publish` folder to the VM
4. Run the application on the VM

#### Option 2: Using Automated Setup Script - Windows VM

1. Copy both `KafkaTestC.exe` and `setup-azure-vm.ps1` to the VM
2. Open PowerShell as Administrator on the VM
3. Navigate to the folder containing the files
4. Run the setup script:
   ```powershell
   .\setup-azure-vm.ps1
   ```
5. The application will be installed to `C:\KafkaTestC`

#### Option 3: Manual Deployment - Linux VM

1. Connect to your Azure Linux VM using SSH
2. Create a folder for the application:
   ```bash
   sudo mkdir -p /opt/KafkaTestC
   ```
3. Copy the `KafkaTestC` file from the `bin\Release\net9.0\linux-x64\publish` folder to the VM
4. Make the file executable:
   ```bash
   sudo chmod +x /opt/KafkaTestC/KafkaTestC
   ```
5. Run the application on the VM:
   ```bash
   /opt/KafkaTestC/KafkaTestC
   ```

#### Option 4: Using Automated Setup Script - Linux VM

1. Copy both `KafkaTestC` executable and `setup-linux-vm.sh` to the VM
2. Make the setup script executable:
   ```bash
   chmod +x setup-linux-vm.sh
   ```
3. Run the setup script with sudo:
   ```bash
   sudo ./setup-linux-vm.sh
   ```
4. The application will be installed to `/opt/KafkaTestC` and a command shortcut `kafka-test-client` will be created

### 3. Configure Azure Managed Identity

1. Ensure your VM has a System or User Assigned Managed Identity
2. Grant the Managed Identity appropriate permissions to access Confluent Cloud resources
3. Verify the application can obtain a token for OAuth authentication

### 4. Testing the Deployment

#### Windows VM
1. Navigate to the application directory:
   ```powershell
   cd C:\KafkaTestC
   ```
2. Run the application:
   ```powershell
   .\KafkaTestC.exe
   ```
3. Choose option 1 (Producer) or 2 (Consumer) to test the connection to Kafka

#### Linux VM
1. Run the application using the provided shortcut:
   ```bash
   kafka-test-client
   ```
   Or navigate to the installation directory:
   ```bash
   cd /opt/KafkaTestC
   ./KafkaTestC
   ```
2. Choose option 1 (Producer) or 2 (Consumer) to test the connection to Kafka

## Troubleshooting

If you encounter authentication issues:
1. Verify that the Managed Identity is properly configured on the VM
2. Check that the identity has appropriate permissions for your Confluent Cloud resources
3. Confirm that the OAuth scope in the application matches your requirements
4. Check the logical cluster ID and identity pool ID values in the application

## Running as a Service

### Windows Service
To run the application as a Windows service:

1. Use NSSM (Non-Sucking Service Manager) or similar tool:
   ```powershell
   # Download NSSM
   Invoke-WebRequest -Uri "https://nssm.cc/release/nssm-2.24.zip" -OutFile "nssm.zip"
   Expand-Archive -Path "nssm.zip" -DestinationPath "C:\nssm"
   
   # Install the service
   C:\nssm\nssm-2.24\win64\nssm.exe install KafkaTestC C:\KafkaTestC\KafkaTestC.exe
   
   # Configure the service
   C:\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC Description "Kafka Test Client for Confluent Cloud"
   C:\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC DisplayName "Kafka Test Client"
   C:\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC Start SERVICE_AUTO_START
   
   # Start the service
   Start-Service KafkaTestC
   ```

2. The service will now run automatically at system startup

### Linux Systemd Service
To run the application as a Linux systemd service:

1. The setup script creates a systemd service file at `/etc/systemd/system/kafka-test-client.service`
2. Enable and start the service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable kafka-test-client
   sudo systemctl start kafka-test-client
   ```

3. Check service status:
   ```bash
   sudo systemctl status kafka-test-client
   ```

4. The service will now run automatically at system startup
