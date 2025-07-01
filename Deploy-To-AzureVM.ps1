# Deploy-To-AzureVM.ps1
# This script helps deploy the Kafka Test Client to an Azure VM
param (
    [Parameter(Mandatory=$true)]
    [string]$VmName,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory=$false)]
    [string]$DestinationPath = "C:\KafkaTestC",
    
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
$publishFolder = Join-Path (Get-Location) "bin\Release\net9.0\win-x64\publish"
$setupScript = Join-Path (Get-Location) "setup-azure-vm.ps1"
$startBatch = Join-Path (Get-Location) "start-kafka-client.bat"

Write-Host "Creating deployment package..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::Open($tempZipFile, [System.IO.Compression.ZipArchiveMode]::Create)

# Add the published EXE
$executablePath = Join-Path $publishFolder "KafkaTestC.exe"
$entry = $zipArchive.CreateEntry("KafkaTestC.exe")
$entryStream = $entry.Open()
$fileStream = [System.IO.File]::OpenRead($executablePath)
$fileStream.CopyTo($entryStream)
$fileStream.Close()
$entryStream.Close()

# Add the setup script
$entry = $zipArchive.CreateEntry("setup-azure-vm.ps1")
$entryStream = $entry.Open()
$fileStream = [System.IO.File]::OpenRead($setupScript)
$fileStream.CopyTo($entryStream)
$fileStream.Close()
$entryStream.Close()

# Add the batch file
$entry = $zipArchive.CreateEntry("start-kafka-client.bat")
$entryStream = $entry.Open()
$fileStream = [System.IO.File]::OpenRead($startBatch)
$fileStream.CopyTo($entryStream)
$fileStream.Close()
$entryStream.Close()

# Create deployment script
$deploymentScript = @"
`$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path '$DestinationPath' -Force | Out-Null
Expand-Archive -Path 'C:\Temp\KafkaTestC.zip' -DestinationPath '$DestinationPath' -Force
cd '$DestinationPath'
.\setup-azure-vm.ps1
"@

if ($InstallAsService) {
    $deploymentScript += @"

# Download NSSM to install as a service
Invoke-WebRequest -Uri "https://nssm.cc/release/nssm-2.24.zip" -OutFile "C:\Temp\nssm.zip"
Expand-Archive -Path "C:\Temp\nssm.zip" -DestinationPath "C:\Temp\nssm"
C:\Temp\nssm\nssm-2.24\win64\nssm.exe install KafkaTestC $DestinationPath\KafkaTestC.exe
C:\Temp\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC Description "Kafka Test Client for Confluent Cloud"
C:\Temp\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC DisplayName "Kafka Test Client"
C:\Temp\nssm\nssm-2.24\win64\nssm.exe set KafkaTestC Start SERVICE_AUTO_START
Start-Service KafkaTestC
"@
}

$entry = $zipArchive.CreateEntry("deploy.ps1")
$entryStream = $entry.Open()
$writer = New-Object System.IO.StreamWriter($entryStream)
$writer.Write($deploymentScript)
$writer.Flush()
$entryStream.Close()

$zipArchive.Dispose()

Write-Host "Uploading and deploying to VM $VmName in resource group $ResourceGroup..."

try {
    # Upload the file to the VM
    $result = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroup -VMName $VmName -CommandId 'RunPowerShellScript' -ScriptString @"
New-Item -ItemType Directory -Path 'C:\Temp' -Force | Out-Null
"@

    # Upload zip to VM
    Set-AzVMCustomScriptExtension -ResourceGroupName $ResourceGroup -VMName $VmName `
        -Name "UploadKafkaTestClient" -Location $(Get-AzVM -ResourceGroupName $ResourceGroup -Name $VmName).Location `
        -FileUri $tempZipFile -Run "powershell.exe -ExecutionPolicy Unrestricted -File deploy.ps1" `
        -ForceRerun $(Get-Random)

    Write-Host "Deployment completed successfully!"
    Write-Host "The application has been deployed to: $DestinationPath"
    if ($InstallAsService) {
        Write-Host "The application has been installed as a service named 'KafkaTestC'"
    } else {
        Write-Host "You can run the application by using the start-kafka-client.bat file"
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
