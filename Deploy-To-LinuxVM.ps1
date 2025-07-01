#!/usr/bin/env pwsh
# Deploy-To-LinuxVM.ps1
# This script helps deploy the Kafka Test Client to a Linux Azure VM
param (
    [Parameter(Mandatory=$true)]
    [string]$VmName,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$false)]
    [string]$DestinationPath = "/opt/KafkaTestC",
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallAsService = $false
)

# Check if Az module is installed
$azModule = Get-Module -Name Az -ListAvailable
if (-not $azModule) {
    Write-Host "Azure PowerShell module not found. Installing..."
    Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force
}

# Import Az modules
Import-Module Az.Accounts
Import-Module Az.Compute

# Check if user is logged in to Azure
$context = Get-AzContext
if (-not $context) {
    Write-Host "Please log in to Azure..."
    Connect-AzAccount
}

# Prepare files for upload
$tempZipFile = [System.IO.Path]::GetTempFileName() + ".zip"
$publishFolder = Join-Path (Get-Location) "bin\Release\net9.0\linux-x64\publish"
$setupScript = Join-Path (Get-Location) "setup-linux-vm.sh"

Write-Host "Creating deployment package..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::Open($tempZipFile, [System.IO.Compression.ZipArchiveMode]::Create)

# Add the published executable
$executablePath = Join-Path $publishFolder "KafkaTestC"
$entry = $zipArchive.CreateEntry("KafkaTestC")
$entryStream = $entry.Open()
$fileStream = [System.IO.File]::OpenRead($executablePath)
$fileStream.CopyTo($entryStream)
$fileStream.Close()
$entryStream.Close()

# Add the setup script
$entry = $zipArchive.CreateEntry("setup-linux-vm.sh")
$entryStream = $entry.Open()
$fileStream = [System.IO.File]::OpenRead($setupScript)
$fileStream.CopyTo($entryStream)
$fileStream.Close()
$entryStream.Close()

# Create deployment script
$deploymentScript = @"
#!/bin/bash
mkdir -p /tmp/kafkatest
unzip -o /tmp/KafkaTestC.zip -d /tmp/kafkatest
chmod +x /tmp/kafkatest/setup-linux-vm.sh
cd /tmp/kafkatest
sudo ./setup-linux-vm.sh
"@

if ($InstallAsService) {
    $deploymentScript += @"

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable kafka-test-client
sudo systemctl start kafka-test-client
"@
}

$entry = $zipArchive.CreateEntry("deploy.sh")
$entryStream = $entry.Open()
$writer = New-Object System.IO.StreamWriter($entryStream)
$writer.Write($deploymentScript)
$writer.Flush()
$entryStream.Close()

$zipArchive.Dispose()

Write-Host "Uploading and deploying to VM $VmName in resource group $ResourceGroup..."

try {
    # Upload the file to the VM
    $result = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroup -VMName $VmName -CommandId 'RunShellScript' -ScriptString @"
mkdir -p /tmp
"@

    # Use Custom Script Extension to upload and run script
    Set-AzVMCustomScriptExtension -ResourceGroupName $ResourceGroup -VMName $VmName `
        -Name "UploadKafkaTestClient" -Location $(Get-AzVM -ResourceGroupName $ResourceGroup -Name $VmName).Location `
        -FileUri $tempZipFile -Run "chmod +x /tmp/deploy.sh && /tmp/deploy.sh" `
        -ForceRerun $(Get-Random)

    Write-Host "Deployment completed successfully!"
    Write-Host "The application has been deployed to: $DestinationPath"
    if ($InstallAsService) {
        Write-Host "The application has been installed as a systemd service named 'kafka-test-client'"
    } else {
        Write-Host "You can run the application using the 'kafka-test-client' command"
    }
} 
catch {
    Write-Host "Error deploying to VM: $_" -ForegroundColor Red
}
finally {
    # Clean up the temp file
    if (Test-Path $tempZipFile) {
        Remove-Item $tempZipFile -Force
    }
}
