@echo off
..\.nuget\nuget pack JoinServerUploader.csproj -IncludeReferencedProjects -Prop "Configuration=Release;Platform=x64"
