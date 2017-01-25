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


add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy


Invoke-WebRequest -Uri "https://localhost:44365/API/Ranker?defaultActions=1,2,3&eventId=1232" -Method Post -Body '{"Age":"Young"}' -Headers @{"Authorization"="obc6wrkthgxla"}