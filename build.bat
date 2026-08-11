@echo off
echo Compilando widgUI...

:: Cierra la instancia previa si esta ejecutandose para liberar el archivo .exe
taskkill /F /IM widgUI.exe >nul 2>&1

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF_DIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF

"%CSC%" /nologo /target:winexe /out:widgUI.exe /lib:"%WPF_DIR%" /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll src\DesktopManager.cs src\MainWindow.cs src\TrayIcon.cs src\Program.cs

if %ERRORLEVEL% EQU 0 (
    echo Compilacion EXITOSA: widgUI.exe creado correctamente.
) else (
    echo Error en la compilacion.
)
