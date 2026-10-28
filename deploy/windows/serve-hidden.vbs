' Starts `grimoire serve` without a console window; stdout/stderr go to %USERPROFILE%\.grimoire\serve.log.
' Used by the "Grimoire Serve" logon task (install-autostart.ps1); safe to double-click as well.
' At logon Docker Desktop (Qdrant) comes up a minute or two after this task, and serve exits on the
' refused connection - so the script waits on serve and restarts it. A run that lived past a minute
' resets the budget; 30 short-lived runs in a row (~8 min) means a real problem, see the log.
Set shell = CreateObject("WScript.Shell")
home = shell.ExpandEnvironmentStrings("%USERPROFILE%")
exe = home & "\.dotnet\tools\grimoire.exe"
logPath = home & "\.grimoire\serve.log"
redirect = " > "
failures = 0

Do While failures < 30
    started = Timer
    ' The outer pair of quotes is for cmd: with more than two quotes on the line it strips the first and the last one.
    shell.Run "cmd /c """"" & exe & """ serve" & redirect & """" & logPath & """ 2>&1""", 0, True
    redirect = " >> "
    lived = Timer - started
    If lived < 0 Then lived = lived + 86400
    If lived > 60 Then failures = 0 Else failures = failures + 1
    WScript.Sleep 15000
Loop
