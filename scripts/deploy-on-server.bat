@echo off
setlocal enabledelayedexpansion

set ZIPDIR=K:\TempDeploy
set SITESDIR=K:\_SITI_ISS
set BACKUPDIR=K:\TempDeploy\Backup

echo Cosa vuoi distribuire?
echo   1) Produzione  (golp + golpapi)
echo   2) Test        (golptest + golpapitest)
echo   0) Esci
set /p CHOICE=Scelta:

if "%CHOICE%"=="1" (
    set ENVNAME=Production
    set API_SITE=golpapi
    set FE_SITE=golp
) else if "%CHOICE%"=="2" (
    set ENVNAME=Testing
    set API_SITE=golpapitest
    set FE_SITE=golptest
) else (
    echo Annullato.
    goto :eof
)

if not exist "%BACKUPDIR%" mkdir "%BACKUPDIR%"

for %%S in (%API_SITE% %FE_SITE%) do (
    set SITEPATH=%SITESDIR%\%%S
    if not exist "!SITEPATH!" (
        echo ERRORE: cartella sito non trovata: !SITEPATH!
        goto :eof
    )
)

set TIMESTAMP=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%

echo.
echo === Arresto siti (%API_SITE%, %FE_SITE%) ===
%windir%\system32\inetsrv\appcmd stop site /site.name:"%API_SITE%"
%windir%\system32\inetsrv\appcmd stop site /site.name:"%FE_SITE%"
%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:"%API_SITE%"

echo.
echo === Backup API (%API_SITE%) ===
powershell -NoProfile -Command "Compress-Archive -Path '%SITESDIR%\%API_SITE%\*' -DestinationPath '%BACKUPDIR%\%API_SITE%-backup-%TIMESTAMP%.zip' -Force"

echo === Backup Frontend (%FE_SITE%) ===
powershell -NoProfile -Command "Compress-Archive -Path '%SITESDIR%\%FE_SITE%\*' -DestinationPath '%BACKUPDIR%\%FE_SITE%-backup-%TIMESTAMP%.zip' -Force"

echo.
echo === Pulizia ed estrazione API (%API_SITE%) ===
set API_ZIP=%ZIPDIR%\golpapi-%ENVNAME%.zip
if not exist "%API_ZIP%" (
    echo ERRORE: zip non trovato: %API_ZIP%
    goto :restart_and_exit
)
powershell -NoProfile -Command "Get-ChildItem -Path '%SITESDIR%\%API_SITE%' -Force | Remove-Item -Recurse -Force"
powershell -NoProfile -Command "Expand-Archive -Path '%API_ZIP%' -DestinationPath '%SITESDIR%\%API_SITE%' -Force"

echo === Pulizia ed estrazione Frontend (%FE_SITE%) ===
set FE_ZIP=%ZIPDIR%\golp-frontend-%ENVNAME%.zip
if not exist "%FE_ZIP%" (
    echo ERRORE: zip non trovato: %FE_ZIP%
    goto :restart_and_exit
)
powershell -NoProfile -Command "Get-ChildItem -Path '%SITESDIR%\%FE_SITE%' -Force | Remove-Item -Recurse -Force"
powershell -NoProfile -Command "Expand-Archive -Path '%FE_ZIP%' -DestinationPath '%SITESDIR%\%FE_SITE%' -Force"

:restart_and_exit
echo.
echo === Riavvio siti (%API_SITE%, %FE_SITE%) ===
%windir%\system32\inetsrv\appcmd start apppool /apppool.name:"%API_SITE%"
%windir%\system32\inetsrv\appcmd start site /site.name:"%API_SITE%"
%windir%\system32\inetsrv\appcmd start site /site.name:"%FE_SITE%"

echo.
echo Fatto. Backup salvati in %BACKUPDIR%
pause
