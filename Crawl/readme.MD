Install Azure Function CLI tools


npm install -g azure-functions-cli

Run on command line from Crawl directory

   %AppData%\npm\func run .


You can attach VS to func.exe.

appsettings.json has the Configuration Manager settings.

(Invoke-WebRequest -Method Post -Body $r.Content -ContentType 'application/json' 'http://localhost:7071/api/CognitiveServiceVision').Content


 curl -v -X POST http://localhost:7071/api/CognitiveServiceVision -H  "Content-Type: application/json" -d @vision.json  -H "Accept: application/json"