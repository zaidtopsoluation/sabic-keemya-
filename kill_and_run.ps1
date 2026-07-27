Write-Output "Killing all Keemya processes..."
Get-Process -Name "Keemya.Frontend" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output "  Killing Keemya.Frontend PID $($_.Id)"
    $_.Kill()
}
Get-Process -Name "keemya-system" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Output "  Killing keemya-system PID $($_.Id)"
    $_.Kill()
}
Start-Sleep -Seconds 2
Write-Output "Launching app..."
Set-Location "C:\Users\HP\Desktop\keemya project"
dotnet run --project Frontend\Keemya.Frontend.csproj
