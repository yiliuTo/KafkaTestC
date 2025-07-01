#!/usr/bin/env pwsh
# Deploy-Full-Linux-Solution.ps1
# Master script to handle the entire deployment process for a Linux VM

param (
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "westus2",
    
    [Parameter(Mandatory=$false)]
    [string]$VMName = "kafka-client-linux-vm",
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallAsService = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipVMCreation = $false
)

$ErrorActionPreference = 'Stop'
$workingDir = Get-Location

Write-Host "========== Kafka Test Client Linux Deployment ==========" -ForegroundColor Cyan
Write-Host "Building application for Linux..." -ForegroundColor Green

# Step 1: Build the application as a self-contained executable for Linux
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true

# Step 2: Deploy VM with managed identity (if not skipped)
if (-not $SkipVMCreation) {
    Write-Host "Deploying Azure Linux VM with managed identity..." -ForegroundColor Green
    & "$workingDir\Deploy-KafkaLinuxVM.ps1" -ResourceGroupName $ResourceGroupName -Location $Location -VMName $VMName
    
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
Write-Host "Deploying Kafka Test Client application to the Linux VM..." -ForegroundColor Green
$deployParams = @{
    VmName = $VMName
    ResourceGroup = $ResourceGroupName
}

if ($InstallAsService) {
    $deployParams.Add("InstallAsService", $true)
}

& "$workingDir\Deploy-To-LinuxVM.ps1" @deployParams

Write-Host "Deployment completed!" -ForegroundColor Green
if ($InstallAsService) {
    Write-Host "The Kafka Test Client has been installed as a systemd service on the VM." -ForegroundColor Cyan
    Write-Host "You can check the service status with: sudo systemctl status kafka-test-client" -ForegroundColor Cyan
} else {
    Write-Host "The Kafka Test Client has been deployed to the VM and can be run using the 'kafka-test-client' command." -ForegroundColor Cyan
}
Write-Host "=================================================" -ForegroundColor Cyan
