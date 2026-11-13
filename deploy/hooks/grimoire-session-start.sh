#!/usr/bin/env bash
# Qwen Code SessionStart hook: hands the Grimoire brief to the runtime as additionalContext.
# Fail-open is the whole contract: every path out of here prints a parseable envelope and exits 0,
# because a session must never wait on Grimoire. The EXIT trap is what makes that promise true even
# for a failure this script did not anticipate.
# Registration is user-scope in ~/.qwen/settings.json; see README for the snippet and its timeout.

set -u

emptyEnvelope='{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":""}}'
work=''
emitted=0

onExit() {
    [ -z "$work" ] || rm -rf "$work"
    [ "$emitted" -eq 1 ] || printf '%s\n' "$emptyEnvelope"
    # Whatever the shell was about to report, a SessionStart hook reports success.
    exit 0
}
trap onExit EXIT

bail() {
    printf '%s\n' "$emptyEnvelope"
    printf 'grimoire-session-start: %s\n' "$1" >&2
    emitted=1
    exit 0
}

# Our own budget in seconds; the runtime caps a command hook at 60s and the README sets that per-hook
# timeout explicitly. Measured here: 0.37s warm, 0.24s against a refused store, and only an unroutable
# endpoint spends the whole budget. HOOK_ prefix, not GRIMOIRE_: the CLI reads every GRIMOIRE_*
# variable as a config overlay, so a name it does not own breaks the call it tunes.
budget=${HOOK_GRIMOIRE_TIMEOUT:-10}
# Validated, never passed through: a non-numeric value makes timeout itself fail (rc 125) and skips both
# the stdin drain and the brief, and 0 is GNU's "no limit" - the unbounded call the budget exists to
# prevent. One line, same acceptance set as the .ps1's TryParse + -gt 0 (10# so a leading zero reads as
# 7, not as octal or as a rejected typo), so one knob value means one thing on both platforms.
[[ "$budget" =~ ^[0-9]+$ ]] && ((10#$budget > 0)) || budget=10

# Drain the payload (session_id, cwd, source, ...) to nothing: leaving it unread can wedge the runtime's
# writer, and none of those fields matter to a brief. The CLI gets /dev/null instead of the leftovers.
if [ ! -t 0 ]; then
    if command -v timeout >/dev/null 2>&1; then
        timeout "$budget" cat >/dev/null 2>&1
    else
        cat >/dev/null 2>&1
    fi
fi

target=${HOOK_GRIMOIRE_BIN:-}
if [ -z "$target" ]; then
    target=$(command -v grimoire 2>/dev/null)
fi
if [ -z "$target" ] && [ -n "${HOME:-}" ]; then
    target=$HOME/.dotnet/tools/grimoire
fi
if [ -z "$target" ] || [ ! -x "$target" ]; then
    bail "grimoire not found on PATH or in ~/.dotnet/tools"
fi

work=$(mktemp -d 2>/dev/null) || bail "no scratch directory"

if command -v timeout >/dev/null 2>&1; then
    timeout -k 1 "$budget" "$target" brief --format hook --daily </dev/null >"$work/out" 2>"$work/err"
else
    "$target" brief --format hook --daily </dev/null >"$work/out" 2>"$work/err"
fi
code=$?

# Non-zero stdout is recovery text, not an envelope - the CLI prints usage help to stdout at exit 2,
# so forwarding it would put a man page in the model's context. Failures stay silent on stdout.
if [ "$code" -ne 0 ]; then
    reason=$(head -n 1 "$work/err" 2>/dev/null)
    [ -n "$reason" ] || reason="brief exited $code"
    bail "$reason"
fi

# grep -c '' counts a final line with no newline too; wc -l would call that file empty and throw a
# good envelope away.
lines=$(grep -c '' "$work/out")
first=$(head -c 1 "$work/out")
if [ "$lines" -ne 1 ] || [ "$first" != "{" ]; then
    bail "brief printed $lines stdout line(s), not one envelope"
fi

cat "$work/out"
emitted=1
