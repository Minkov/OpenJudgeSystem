NET STOP "Stage OJS Local Worker Service"
sc delete "Stage OJS Local Worker Service"
timeout 10
CD %~dp0
C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil "..\OJS.Workers.LocalWorker.exe"
NET START "Stage OJS Local Worker Service"
pause