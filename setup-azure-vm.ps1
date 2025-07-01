# Setup script for Kafka Test Client on Azure VM

# Create a directory for the application
$appDir = "C:\KafkaTestC"
if (-not (Test-Path $appDir)) {
    Write-Host "Creating application directory: $appDir"
    New-Item -ItemType Directory -Path $appDir -Force | Out-Null
}

# Copy files from the current directory to the application directory
Write-Host "Copying application files to $appDir"
Copy-Item -Path "KafkaTestC.exe" -Destination $appDir -Force

# Set up a scheduled task to run at startup (optional)
$taskName = "KafkaTestC-Startup"
$taskExists = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($taskExists) {
    Write-Host "Removing existing scheduled task: $taskName"
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Create a firewall rule for the application (if needed)
$firewallRuleName = "KafkaTestC"
$firewallRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue

if ($firewallRule) {
    Write-Host "Removing existing firewall rule: $firewallRuleName"
    Remove-NetFirewallRule -DisplayName $firewallRuleName
}

Write-Host "Creating firewall rule: $firewallRuleName"
New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort 9092 -Description "Allow Kafka Client"

Write-Host "Setup completed successfully!"
Write-Host "You can run the application by navigating to $appDir and running KafkaTestC.exe"
