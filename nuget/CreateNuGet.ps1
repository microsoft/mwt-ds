param([string]$apiKey="", [string]$outputDirectory=".")

$version = Get-Content version.txt

..\packages\gitlink.2.2.0\lib\net45\GitLink.exe .. -f Decision.sln -u https://github.com/multiworldtesting/decision -p x64 -c Release -d ..\bin\x64\Release

..\.nuget\nuget pack ..\ClientDecisionService\ClientDecisionService.csproj -Version $version -IncludeReferencedProjects -Prop "Configuration=Release;Platform=x64" -OutputDirectory $outputDirectory
..\.nuget\nuget pack ..\JoinServerUploader\JoinServerUploader.csproj -Version $version -Prop "Configuration=Release;Platform=x64" -OutputDirectory $outputDirectory
..\.nuget\nuget pack ..\MultiWorldTestingServiceContract\MultiWorldTestingServiceContract.csproj -Version $version -Prop "Configuration=Release;Platform=x64" -OutputDirectory $outputDirectory


if (-not [string]::IsNullOrEmpty($apiKey))
{ 
    ..\.nuget\nuget push Microsoft.Research.MultiWorldTesting.*.nupkg $apiKey
}
