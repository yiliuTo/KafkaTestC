# Deploy-Full-Solution.ps1
# Master script to handle the entire deployment process

param (
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "westus2",
    
    [Parameter(Mandatory=$false)]
    [string]$VMName = "kafka-client-vm",
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallAsService = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipVMCreation = $false
)

$ErrorActionPreference = 'Stop'
$workingDir = Get-Location

Write-Host "========== Kafka Test Client Deployment ==========" -ForegroundColor Cyan
Write-Host "Building application..." -ForegroundColor Green

# Step 1: Build the application as a self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true

# Step 2: Deploy VM with managed identity (if not skipped)
if (-not $SkipVMCreation) {
    Write-Host "Deploying Azure VM with managed identity..." -ForegroundColor Green
    & "$workingDir\Deploy-KafkaVM.ps1" -ResourceGroupName $ResourceGroupName -Location $Location -VMName $VMName
    
    # Ask user if they have configured the managed identity permissions
    Write-Host "Important:" -ForegroundColor Yellow
    Write-Host "Before continuing, please ensure you have assigned the necessary role to the Managed Identity"
    Write-Host "for accessing Confluent Cloud resources." -ForegroundColor Yellow
    $confirmation = Read-Host "Have you configured the Managed Identity permissions? (Y/N)"
    
    if ($confirmation -ne "Y" -and $confirmation -ne "y") {
        Write-Host "Please configure the Managed Identity permissions before deploying the application." -ForegroundColor Red
        Write-Host "Exiting deployment process." -ForegroundColor Red
        return
    }
}

# Step 3: Deploy the application to the VM
Write-Host "Deploying Kafka Test Client application to the VM..." -ForegroundColor Green
$deployParams = @{
    VmName = $VMName
    ResourceGroup = $ResourceGroupName
}

if ($InstallAsService) {
    $deployParams.Add("InstallAsService", $true)
}

& "$workingDir\Deploy-To-AzureVM.ps1" @deployParams

Write-Host "Deployment completed!" -ForegroundColor Green
if ($InstallAsService) {
    Write-Host "The Kafka Test Client has been installed as a Windows service on the VM." -ForegroundColor Cyan
} else {
    Write-Host "The Kafka Test Client has been deployed to the VM and can be run using the start-kafka-client.bat file." -ForegroundColor Cyan
}
Write-Host "=================================================" -ForegroundColor Cyan
