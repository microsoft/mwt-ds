$url = Read-Host -Prompt "Management Center URL"
$password = Read-Host -Prompt "Password" 
Invoke-WebRequest -Uri "$($url)/Automation/AppSettings" -Headers @{"auth"=$password} -OutFile ..\..\AppSettingsSecrets.config