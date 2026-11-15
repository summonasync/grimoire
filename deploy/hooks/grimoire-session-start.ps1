# Qwen Code SessionStart hook: hands the Grimoire brief to the runtime as additionalContext.
# Fail-open is the whole contract: every path out of here prints a parseable envelope and exits 0,
# because a session must never wait on Grimoire. The outer trap is what makes that hold for failures
# this script did not anticipate either.
# Register it user-scope: "command": "powershell -NoProfile -ExecutionPolicy Bypass -File <path>"

$emptyEnvelope = '{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}'
$emitted = $false
$stdinStream = $null

function Release-Stdin {
    # Idempotent: the drain calls it as soon as it stops waiting, so every later path (brief, bail,
    # outer trap) already has the stream gone.
    if ($script:stdinStream) {
        try { $script:stdinStream.Dispose() } catch { }
        $script:stdinStream = $null
    }
}

function Send-EmptyEnvelope([string]$reason) {
    $script:emptyEnvelope | Write-Output
    # Console.Error, not Write-Error: the cmdlet decorates the line with the function name and script
    # position, and the contract here is one short line a human can read in a hook log.
    if ($reason) { [Console]::Error.WriteLine("grimoire-session-start: $reason") }
    $script:emitted = $true
    exit 0
}

# Our own budget in seconds; the runtime caps a command hook at 60s and the README sets that per-hook
# timeout explicitly. HOOK_ prefix, not GRIMOIRE_: the CLI reads every GRIMOIRE_* variable as a config
# overlay, so a name it does not own breaks the call it tunes. Both platforms read the same knob and the
# same range: at or above 60 the runtime would cut us first, so the value could not mean what it says, and
# an int near the ceiling was worse than meaningless - 2147484 passed the old guard, then the *1000
# millisecond conversions overflowed and the brief was dropped silently at 504ms. Rejecting it up front
# costs nothing and keeps the twins agreeing on every input.
$budgetSeconds = 10

try {
    if ($env:HOOK_GRIMOIRE_TIMEOUT) {
        $parsed = 0
        if ([int]::TryParse($env:HOOK_GRIMOIRE_TIMEOUT, [ref]$parsed) -and $parsed -gt 0 -and $parsed -lt 60) {
            $budgetSeconds = $parsed
        }
    }

    # Drain the payload to nothing: none of its fields matter to a brief, and a pipe nobody reads can
    # wedge its writer. Deliberately NOT [Console]::In.ReadToEndAsync() - that pump never gives control
    # back while a writer holds stdin open, so the script never reaches the brief at all: review 046-08
    # measured 20 187/15 884 ms at budgets 3/2 against the .sh twin's 3 012 ms, and re-running the same
    # bytes here, the read had still not returned at a 35 s cap. The raw stream read with a bounded Wait
    # does both jobs: it drained the full 200 KB fixture and abandoned at the budget.
    if ([Console]::IsInputRedirected) {
        $buffer = New-Object byte[] 16384
        $script:stdinStream = [Console]::OpenStandardInput()
        $clock = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            while ($true) {
                $left = ($budgetSeconds * 1000) - $clock.ElapsedMilliseconds
                if ($left -le 50) { break }
                $read = $script:stdinStream.ReadAsync($buffer, 0, $buffer.Length)
                if (-not $read.Wait([int]$left)) { break }
                if ($read.Result -le 0) { break }
            }
        } catch { }
        # The abandoned read stays pending on the fd, and that is what would hold the process at exit.
        Release-Stdin
    }

    $target = $env:HOOK_GRIMOIRE_BIN
    if (-not $target) {
        $found = Get-Command grimoire -ErrorAction SilentlyContinue
        if ($found) { $target = $found.Source }
    }
    if (-not $target -and $env:USERPROFILE) {
        $candidate = Join-Path $env:USERPROFILE '.dotnet\tools\grimoire.exe'
        if (Test-Path $candidate) { $target = $candidate }
    }
    if (-not $target -or -not (Test-Path $target)) {
        Send-EmptyEnvelope 'grimoire not found on PATH or in %USERPROFILE%\.dotnet\tools'
    }

    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $target
    $start.Arguments = 'brief --format hook --daily'
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $start.UseShellExecute = $false

    $stdout = ''
    $stderr = ''
    $exitCode = 0
    $exited = $false
    $process = New-Object System.Diagnostics.Process
    try {
        $process = [System.Diagnostics.Process]::Start($start)
        $readOut = $process.StandardOutput.ReadToEndAsync()
        $readErr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Close()
        if ($process.WaitForExit($budgetSeconds * 1000)) {
            $exited = $true
            $stdout = $readOut.GetAwaiter().GetResult()
            $stderr = $readErr.GetAwaiter().GetResult()
            $exitCode = $process.ExitCode
        } else {
            try { $process.Kill($true) } catch { }
        }
    } finally {
        if ($process) { $process.Dispose() }
    }

    if (-not $exited) { Send-EmptyEnvelope "brief did not answer in ${budgetSeconds}s" }
    if ($exitCode -ne 0) {
        # Non-zero stdout is recovery text, not an envelope - the CLI prints usage help to stdout at
        # exit 2, so forwarding it would put a man page in the model's context.
        $first = @($stderr -split "`r?`n" | Where-Object { $_ }) | Select-Object -First 1
        if (-not $first) { $first = "brief exited $exitCode" }
        Send-EmptyEnvelope $first
    }

    $lines = @($stdout -split "`r?`n" | Where-Object { $_ -ne '' })
    if ($lines.Count -ne 1 -or -not $lines[0].StartsWith('{')) {
        Send-EmptyEnvelope "brief printed $($lines.Count) stdout line(s), not one envelope"
    }

    # The CLI's own line, handed over as-is: nothing reformats between the store and the model.
    [Console]::Out.WriteLine($lines[0])
    $emitted = $true
    exit 0
} catch {
    if (-not $emitted) { Send-EmptyEnvelope $_.Exception.Message }
    exit 0
}
