NET STOP "Stage OJS Agent Service"
sc delete "Stage OJS Agent Service"
timeout 10
CD %~dp0
C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil "..\OJS.Workers.Agent.exe"
NET START "Stage OJS Agent Service"
pause