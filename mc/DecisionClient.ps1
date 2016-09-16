$url="https://mc-mcasaqm47tzmvkmqvs.azurewebsites.net" 
$auth="oxi24wl5qdvni"

$resp = Invoke-RestMethod -Headers @{"auth"=$auth} -Method Post -Uri "$url/API/Policy" -Body '{"Age":"Young"}'
$eventId = $resp.EventId

Write-Host $eventId
Invoke-RestMethod -Headers @{"auth"=$auth} -Method Post -Uri "$url/API/Reward?eventId=$eventId" -Body "1"
sleep -Seconds 1
Invoke-RestMethod -Headers @{"auth"=$auth} -Method Post -Uri "$url/API/Reward?eventId=$eventId" -Body "2"
sleep -Seconds 1
Invoke-RestMethod -Headers @{"auth"=$auth} -Method Post -Uri "$url/API/Reward?eventId=$eventId" -Body "3"

# Invoke-WebRequest -Uri "http://trainer-mcdel5spqfpsfzclank.cloudapp.net/reset" -Headers @{"Authorization"="5luyyzwmoh3fw"}

get-help invoke-web

Invoke-WebRequest -Uri "http://trainer-mcdel5spqfpsfzclank.cloudapp.net/reset" -Headers @{"Authorization"="5luyyzwmoh3fw"} -Method Put

Invoke-WebRequest -Uri "http://trainer-eujul3v6w5nggdvlwlpoxc42g6.cloudapp.net/reset" -Headers @{"Authorization"="ms75bjrsyvbhc"} -Method Post


