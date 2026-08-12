@echo off
echo Compilando widgUI...

:: Cierra la instancia previa si esta ejecutandose para liberar el archivo .exe
taskkill /F /IM widgUI.exe >nul 2>&1

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF_DIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF
set WINMD=C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd
set SRWR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll
set LIB_DIR=%~dp0lib
set SR_DLL=%LIB_DIR%\System.Runtime.dll

if not exist "%LIB_DIR%" mkdir "%LIB_DIR%"
if not exist "%SR_DLL%" (
    echo Descargando System.Runtime.dll...
    curl.exe -L -o "%TEMP%\system.runtime.zip" "https://www.nuget.org/api/v2/package/System.Runtime/4.3.1" >nul
    powershell -NoProfile -Command "Expand-Archive -Path '%TEMP%\system.runtime.zip' -DestinationPath '%TEMP%\system.runtime' -Force"
    copy /Y "%TEMP%\system.runtime\lib\net462\System.Runtime.dll" "%SR_DLL%" >nul
)

if not exist "%WINMD%" (
    echo No se encontro Windows.winmd. Instala Windows SDK 10.
    exit /b 1
)

"%CSC%" /nologo /target:winexe /out:widgUI.exe /lib:"%WPF_DIR%" /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll /r:System.Runtime.Serialization.dll /r:"%WINMD%" /r:"%SR_DLL%" /r:"%SRWR%" src\DesktopManager.cs src\MainWindow.cs src\EdgeMenuWindow.cs src\FolderWidgetWindow.cs src\ExpandedFolderWidgetWindow.cs src\CalendarWidgetWindow.cs src\ImageWidgetWindow.cs src\SystemMediaHelper.cs src\MusicWidgetWindow.cs src\DockWidgetWindow.cs src\CustomClockWidgetWindow.cs src\AppWidgetWindow.cs src\LayoutProfile.cs src\ProfileService.cs src\WidgetRegistry.cs src\WidgetAppearanceHelper.cs src\TrayIcon.cs src\IconHelper.cs src\Program.cs

if %ERRORLEVEL% EQU 0 (
    if not exist widgUI.exe.config (
        (
            echo ^<?xml version="1.0" encoding="utf-8"?^>
            echo ^<configuration^>
            echo   ^<runtime^>
            echo     ^<assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"^>
            echo       ^<dependentAssembly^>
            echo         ^<assemblyIdentity name="System.Runtime" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" /^>
            echo         ^<bindingRedirect oldVersion="0.0.0.0-4.1.1.1" newVersion="4.1.1.1" /^>
            echo       ^</dependentAssembly^>
            echo     ^</assemblyBinding^>
            echo   ^</runtime^>
            echo ^</configuration^>
        ) > widgUI.exe.config
    )
    copy /Y "%SR_DLL%" . >nul 2>&1
    echo Compilacion EXITOSA: widgUI.exe creado correctamente.
) else (
    echo Error en la compilacion.
)
