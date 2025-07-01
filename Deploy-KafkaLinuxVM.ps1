#!/usr/bin/env pwsh
# Deploy-KafkaLinuxVM.ps1
# This script deploys an Azure Linux VM with managed identity for running the Kafka Test Client

param (
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "westus2",
    
    [Parameter(Mandatory=$false)]
    [string]$VMName = "kafka-client-linux-vm",
    
    [Parameter(Mandatory=$false)]
    [string]$VMSize = "Standard_B2s",
    
    [Parameter(Mandatory=$false)]
    [string]$AdminUsername = "kafkaadmin"
)

$ErrorActionPreference = 'Stop'

# Check if Az module is installed
$azModule = Get-Module -Name Az -ListAvailable
if (-not $azModule) {
    Write-Host "Azure PowerShell module not found. Installing..."
    Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force
}

# Import Az modules
Import-Module Az.Accounts
Import-Module Az.Compute
Import-Module Az.Network
Import-Module Az.Resources

# Check if user is logged in to Azure
$context = Get-AzContext
if (-not $context) {
    Write-Host "Please log in to Azure..."
    Connect-AzAccount
}

# Create resource group if it doesn't exist
$resourceGroup = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (-not $resourceGroup) {
    Write-Host "Creating resource group $ResourceGroupName in $Location..."
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
}

# Generate a secure password for the VM
$vmPasswordLength = 20
$nonAlphaChars = 5
$password = [System.Web.Security.Membership]::GeneratePassword($vmPasswordLength, $nonAlphaChars)
$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential ($AdminUsername, $securePassword)

# Create virtual network and subnet
$subnetConfig = New-AzVirtualNetworkSubnetConfig -Name "KafkaSubnet" -AddressPrefix "10.0.1.0/24"
$vnet = New-AzVirtualNetwork -ResourceGroupName $ResourceGroupName -Location $Location `
    -Name "KafkaVNet" -AddressPrefix "10.0.0.0/16" -Subnet $subnetConfig

# Create a public IP address
$pip = New-AzPublicIpAddress -ResourceGroupName $ResourceGroupName -Location $Location `
    -Name "KafkaPublicIP" -AllocationMethod Dynamic -IdleTimeoutInMinutes 4

# Create network security group and rules
$nsgRuleSSH = New-AzNetworkSecurityRuleConfig -Name "AllowSSH" -Protocol Tcp `
    -Direction Inbound -Priority 1000 -SourceAddressPrefix * -SourcePortRange * `
    -DestinationAddressPrefix * -DestinationPortRange 22 -Access Allow
$nsg = New-AzNetworkSecurityGroup -ResourceGroupName $ResourceGroupName -Location $Location `
    -Name "KafkaNSG" -SecurityRules $nsgRuleSSH

# Create a virtual network card and associate it with public IP address and NSG
$nic = New-AzNetworkInterface -Name "KafkaNIC" -ResourceGroupName $ResourceGroupName -Location $Location `
    -SubnetId $vnet.Subnets[0].Id -PublicIpAddressId $pip.Id -NetworkSecurityGroupId $nsg.Id

# Define VM configuration
$vmConfig = New-AzVMConfig -VMName $VMName -VMSize $VMSize | 
    Set-AzVMOperatingSystem -Linux -ComputerName $VMName -Credential $credential -DisablePasswordAuthentication |
    Set-AzVMSourceImage -PublisherName "Canonical" -Offer "UbuntuServer" -Skus "18.04-LTS" -Version "latest" |
    Add-AzVMNetworkInterface -Id $nic.Id

# Configure SSH key
$sshPublicKey = Get-Content ~/.ssh/id_rsa.pub -ErrorAction SilentlyContinue
if ($sshPublicKey) {
    Add-AzVMSshPublicKey -VM $vmConfig -KeyData $sshPublicKey -Path "/home/$AdminUsername/.ssh/authorized_keys"
} else {
    Write-Host "No SSH public key found at ~/.ssh/id_rsa.pub. SSH will be configured with password authentication."
    $vmConfig = Set-AzVMOperatingSystem -VM $vmConfig -Linux -ComputerName $VMName -Credential $credential
}

# Create the VM with system assigned managed identity
$vm = New-AzVM -ResourceGroupName $ResourceGroupName -Location $Location -VM $vmConfig -IdentityType SystemAssigned

# Output details
Write-Host "VM created successfully!" -ForegroundColor Green
Write-Host "VM Name: $VMName"
Write-Host "Resource Group: $ResourceGroupName"
Write-Host "Admin Username: $AdminUsername"
if (-not $sshPublicKey) {
    Write-Host "Admin Password: $password"
    Write-Host "Please save this password in a secure location!"
}

# Get VM's public IP
$publicIpAddress = (Get-AzPublicIpAddress -ResourceGroupName $ResourceGroupName -Name "KafkaPublicIP").IpAddress
Write-Host "Public IP Address: $publicIpAddress"

# Get managed identity details
$vm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $VMName
$identity = $vm.Identity
Write-Host "Managed Identity Principal ID: $($identity.PrincipalId)"
Write-Host "Managed Identity Tenant ID: $($identity.TenantId)"

Write-Host "Important: Make sure to assign appropriate role to the managed identity for accessing Confluent Cloud resources." -ForegroundColor Yellow
