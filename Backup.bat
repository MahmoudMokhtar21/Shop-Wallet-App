@echo off
setlocal
set "SHOPWALLET_BACKUP_ROOT=%~dp0"
echo Close ShopWalletApp before running this backup.
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; try { $root=$env:SHOPWALLET_BACKUP_ROOT; $source=Join-Path $root 'ShopWalletDB.db'; if (Get-Process -Name 'ShopWalletApp' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $root 'ShopWalletApp.exe') }) { throw 'Close ShopWalletApp and run Backup.bat again.' }; if (-not (Test-Path -LiteralPath $source)) { throw 'ShopWalletDB.db was not found. Run the application first.' }; $wal=$source+'-wal'; if ((Test-Path -LiteralPath $wal) -and (Get-Item -LiteralPath $wal).Length -gt 0) { throw 'SQLite has pending WAL data. Open and close the app normally, then retry.' }; $folder=Join-Path $root 'Backups'; [IO.Directory]::CreateDirectory($folder) | Out-Null; $dest=Join-Path $folder ('ShopWalletDB_'+([DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff',[Globalization.CultureInfo]::InvariantCulture))+'.db'); $inputFile=[IO.File]::Open($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); try { $outputFile=[IO.File]::Open($dest,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None); try { $inputFile.CopyTo($outputFile) } finally { $outputFile.Dispose() } } finally { $inputFile.Dispose() }; Write-Host ('Backup Done: '+$dest) } catch { Write-Host ('Backup FAILED: '+$_.Exception.Message); exit 1 }"
if errorlevel 1 (
  pause
  exit /b 1
)
pause
exit /b 0
