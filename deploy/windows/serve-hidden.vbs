' Starts `grimoire serve` without a console window; stdout/stderr go to %USERPROFILE%\.grimoire\serve.log.
' Used by the "Grimoire Serve" logon task (install-autostart.ps1); safe to double-click as well.
Set shell = CreateObject("WScript.Shell")
home = shell.ExpandEnvironmentStrings("%USERPROFILE%")
exe = home & "\.dotnet\tools\grimoire.exe"
logPath = home & "\.grimoire\serve.log"
' The outer pair of quotes is for cmd: with more than two quotes on the line it strips the first and the last one.
shell.Run "cmd /c """"" & exe & """ serve > """ & logPath & """ 2>&1""", 0, False
