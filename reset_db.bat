@echo off
echo Dang Reset Database va Nap lai du lieu mau R-WFM (.NET 8)...
dotnet run --project API\API.csproj --urls "http://localhost:5050" -- --reset-db
pause
