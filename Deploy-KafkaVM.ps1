# Deploy-KafkaVM.ps1
# Script to deploy a VM with managed identity for the Kafka Test Client

param (
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "westus2",
    
    [Parameter(Mandatory=$false)]
    [string]$VMName = "kafka-client-vm",
    
    [Parameter(Mandatory=$false)]
    [string]$AdminUsername = "kafkaadmin"
)

# Check if Az module is installed
$azModule = Get-Module -Name Az -ListAvailable
if (-not $azModule) {
    Write-Host "Azure PowerShell module not found. Installing..."
    Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force
}

# Import Az modules
Import-Module Az.Accounts
Import-Module Az.Resources

# Check if user is logged in to Azure
$context = Get-AzContext
if (-not $context) {
    Write-Host "Please log in to Azure..."
    Connect-AzAccount
}

# Check if resource group exists, create if not
$resourceGroup = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (-not $resourceGroup) {
    Write-Host "Creating resource group $ResourceGroupName in $Location..."
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
}

# Get password securely
$adminPassword = Read-Host -Prompt "Enter password for VM admin user" -AsSecureString

# Deploy the template
$templateFile = Join-Path (Get-Location) "azure-vm-template.json"
$deploymentName = "KafkaVM-Deployment-$(Get-Date -Format 'yyyyMMddHHmmss')"

Write-Host "Deploying VM with managed identity..."
$deployment = New-AzResourceGroupDeployment `
    -Name $deploymentName `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile $templateFile `
    -vmName $VMName `
    -adminUsername $AdminUsername `
    -adminPassword $adminPassword

if ($deployment.ProvisioningState -eq "Succeeded") {
    Write-Host "VM deployed successfully!" -ForegroundColor Green
    
    # Get the Managed Identity Principal ID
    $principalId = $deployment.Outputs.principalId.Value
    Write-Host "VM Managed Identity Principal ID: $principalId"
    
    # Output instructions for next steps
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Assign the necessary role to the Managed Identity for Confluent Cloud access"
    Write-Host "   Using the Principal ID: $principalId"
    Write-Host "2. Deploy the Kafka Test Client to the VM using the Deploy-To-AzureVM.ps1 script:"
    Write-Host "   .\Deploy-To-AzureVM.ps1 -VmName $VMName -ResourceGroup $ResourceGroupName"
    
    # Show the IP address to connect to
    $publicIp = $deployment.Outputs.publicIP.Value
    Write-Host "You can connect to the VM at: $publicIp"
} else {
    Write-Host "Deployment failed: $($deployment.Error)" -ForegroundColor Red
}
