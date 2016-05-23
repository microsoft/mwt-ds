$version = Get-Content version.txt

function PatchAssemblyInfo ($file)
{
    $text = (Get-Content $file) -replace "Version\(""[0-9\.]+""\)", "Version(""$version"")"
    Set-Content -Path $file -Value $text
}

PatchAssemblyInfo "..\ClientDecisionService\Properties\AssemblyInfo.cs"
PatchAssemblyInfo "..\JoinServerUploader\Properties\AssemblyInfo.cs"
PatchAssemblyInfo "..\MultiWorldTestingServiceContract\Properties\AssemblyInfo.cs"
