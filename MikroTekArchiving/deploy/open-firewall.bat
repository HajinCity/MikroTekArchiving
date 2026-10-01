@echo off
rem ------------------------------------------------------------------
rem  Allows the other PCs on the same network to reach port 5000.
rem  Right-click > "Run as administrator". Run once on the server PC.
rem ------------------------------------------------------------------
net session >nul 2>&1
if errorlevel 1 (
  echo Please right-click open-firewall.bat and choose "Run as administrator".
  pause
  exit /b 1
)

set RULE=MikroTek Archiving Portal (TCP 5000)
netsh advfirewall firewall delete rule name="%RULE%" >nul 2>&1
rem remoteip=localsubnet: only PCs on the same local network are allowed, not the internet.
netsh advfirewall firewall add rule name="%RULE%" dir=in action=allow protocol=TCP localport=5000 remoteip=localsubnet profile=any
if errorlevel 1 (
  echo Could not add the firewall rule.
) else (
  echo Firewall rule added: other PCs on this network can now open port 5000.
)
pause
