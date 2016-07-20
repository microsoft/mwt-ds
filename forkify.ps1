param ([string]$fork = "Microsoft")


$f = (gc provisioning\azuredeploy.json)
$f = ($f -replace 'https://raw.githubusercontent.com/.*/mwt-ds/master/provisioning/', "https://raw.githubusercontent.com/$fork/mwt-ds/master/provisioning/")
$f | Out-File provisioning\azuredeploy.json

$f = (gc provisioning\test\ProvisioningBaseTest.cs)
$f = ($f -replace "https://raw.githubusercontent.com/.*/mwt-ds/master/provisioning", "https://raw.githubusercontent.com/$fork/mwt-ds/master/provisioning")
$f | Out-File provisioning\test\ProvisioningBaseTest.cs

$f = (gc provisioning\templates\WebManageTemplate.json)
$f = ($f -replace "https://github.com/.*/mwt-ds.git", "https://github.com/$fork/mwt-ds.git")
$f | Out-File provisioning\templates\WebManageTemplate.json

$f = (gc provisioning\templates\WebManageUpgradeTemplate.json)
$f = ($f -replace "https://github.com/.*/mwt-ds.git", "https://github.com/$fork/mwt-ds.git")
$f | Out-File provisioning\templates\WebManageUpgradeTemplate.json
