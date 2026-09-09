#!/usr/bin/env bash
# Tests for scripts/agent/dotnet-capability.sh — the SessionStart .NET capability probe.
#
# The hook's whole value is that its verdict is *measured*, so these tests drive it against a
# fabricated environment rather than the host's: PATH is replaced with a directory of stubs, and the
# stubs' behaviour is set per case. Nothing here touches the real network or the real ~/.dotnet.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/../dotnet-capability.sh"

PASS=0
FAIL=0

fail() {
  FAIL=$((FAIL + 1))
  printf '  \033[31mFAIL\033[0m %s\n' "$1"
  [ $# -gt 1 ] && printf '       %s\n' "$2"
  return 0
}

pass() {
  PASS=$((PASS + 1))
  printf '  \033[32mok\033[0m   %s\n' "$1"
}

assert_contains() {
  local haystack=$1 needle=$2 what=$3
  case "$haystack" in
    *"$needle"*) pass "$what" ;;
    *) fail "$what" "expected to contain: $needle" ;;
  esac
}

assert_not_contains() {
  local haystack=$1 needle=$2 what=$3
  case "$haystack" in
    *"$needle"*) fail "$what" "expected NOT to contain: $needle" ;;
    *) pass "$what" ;;
  esac
}

assert_equals() {
  local actual=$1 expected=$2 what=$3
  if [ "$actual" = "$expected" ]; then
    pass "$what"
  else
    fail "$what" "expected [$expected], got [$actual]"
  fi
}

# --- environment fabrication -------------------------------------------------------------------

# Builds a throwaway HOME, a stub bin dir on PATH, and exports the hook's knobs at them.
new_env() {
  # An explicit template, not a bare `mktemp -d`: on macOS the bare form ignores TMPDIR and uses
  # the confstr path under /var/folders, which an agent sandbox will not let us write to.
  SANDBOX="$(mktemp -d "${TMPDIR:-/tmp}/motiv-dotnet-hook.XXXXXX")"
  export SANDBOX
  mkdir -p "$SANDBOX/bin" "$SANDBOX/hostbin" "$SANDBOX/home" "$SANDBOX/state"
  export HOME="$SANDBOX/home"
  export MOTIV_DOTNET_HOOK_STATE_DIR="$SANDBOX/state"
  export MOTIV_DOTNET_HOOK_INSTALL_ROOT="$SANDBOX/home/.dotnet"
  unset MOTIV_DOTNET_HOOK_SKIP MOTIV_DOTNET_HOOK_FORCE DOTNET_ROOT 2>/dev/null || true
  # A fabricated PATH, and the hazard it has to survive. The stub dir comes first, then a dir
  # standing in for a host that already has .NET, then the real coreutils the script needs.
  #
  # `hostbin` exists because leaving the host's own PATH underneath the stubs made this suite pass
  # for the wrong reason: GitHub's ubuntu runners ship a real `dotnet` on /usr/bin, so `command -v
  # dotnet` found a genuine SDK 10 and 24 assertions went red in CI while green on a Mac, where the
  # muxer lives somewhere /usr/bin does not reach. Every case now runs with a working muxer sitting
  # on PATH behind the stubs, so the shadowing is exercised continuously rather than assumed.
  export PATH="$SANDBOX/bin:$SANDBOX/hostbin:/usr/bin:/bin:/usr/sbin:/sbin"
  stub_muxer "$SANDBOX/hostbin/dotnet" "10.0.999"
  # And a `dotnet` that reports no SDKs, shadowing it. Cases wanting a usable one overwrite this.
  stub_muxer "$SANDBOX/bin/dotnet" ""
  # Every case gets a failing curl by default, so no case can silently reach the real network — a
  # test suite that does is both slow and a liar about what it proved.
  stub_curl 6
}

drop_env() { [ -n "${SANDBOX:-}" ] && rm -rf "$SANDBOX"; }

# Writes a `dotnet` muxer stub at $1 that reports the SDK versions in $2 (space separated).
stub_muxer() {
  local path=$1 sdks=$2
  mkdir -p "$(dirname "$path")"
  {
    echo '#!/usr/bin/env bash'
    # shellcheck disable=SC2016  # single quotes are deliberate: this is a script being written, not run
    echo 'if [ "${1:-}" = "--list-sdks" ]; then'
    for v in $sdks; do
      echo "  echo '$v [/sdks]'"
    done
    echo '  exit 0'
    echo 'fi'
    echo 'exit 1'
  } >"$path"
  chmod +x "$path"
}

# Writes a `curl` stub. $1 is the exit code it reports. When 0, it writes an install-script stub to
# the path given after -o; that install-script stub appends its own arguments to $SANDBOX/install.log
# and creates the muxer unless the channel appears in $SANDBOX/install-fail.
stub_curl() {
  local rc=$1
  cat >"$SANDBOX/bin/curl" <<CURL
#!/usr/bin/env bash
out=""
prev=""
for a in "\$@"; do
  [ "\$prev" = "-o" ] && out="\$a"
  prev="\$a"
done
echo "curl \$*" >>"$SANDBOX/curl.log"
if [ "$rc" != "0" ]; then exit $rc; fi
cat >"\$out" <<'INNER'
#!/usr/bin/env bash
echo "install \$*" >>"__LOG__"
channel=""
prev=""
for a in "\$@"; do
  [ "\$prev" = "--channel" ] && channel="\$a"
  prev="\$a"
done
if [ -f "__FAILFILE__" ] && grep -qx "\$channel" "__FAILFILE__"; then exit 7; fi
# A real dotnet-install.sh can exit 0 having installed nothing usable — a channel that resolves to
# no build for the platform is the everyday way. The hook must not take exit 0 as proof.
if [ -f "__NOOPFILE__" ] && grep -qx "\$channel" "__NOOPFILE__"; then exit 0; fi
mkdir -p "__ROOT__"
cat >"__ROOT__/dotnet" <<'MUX'
#!/usr/bin/env bash
if [ "\${1:-}" = "--list-sdks" ]; then echo "10.0.203 [/sdks]"; exit 0; fi
exit 1
MUX
chmod +x "__ROOT__/dotnet"
INNER
sed -i.bak -e "s#__LOG__#$SANDBOX/install.log#g" \
           -e "s#__FAILFILE__#$SANDBOX/install-fail#g" \
           -e "s#__NOOPFILE__#$SANDBOX/install-noop#g" \
           -e "s#__ROOT__#\$MOTIV_DOTNET_HOOK_INSTALL_ROOT#g" "\$out"
rm -f "\$out.bak"
exit 0
CURL
  chmod +x "$SANDBOX/bin/curl"
}

json_field() {
  python3 -c '
import json,sys
d = json.load(sys.stdin)
for k in sys.argv[1].split("."):
    d = d[k]
print(d)
' "$1"
}

run_hook() { bash "$HOOK" </dev/null 2>"$SANDBOX/stderr"; }

# Runs the hook and echoes the one field almost every case asserts against: the prose the session
# will actually be shown.
hook_context() { run_hook | json_field hookSpecificOutput.additionalContext; }

# How many times the curl stub has been called — how the replay cases tell a fresh probe from a
# recorded one.
curl_calls() { wc -l <"$SANDBOX/curl.log" | tr -d ' '; }

# --- cases -------------------------------------------------------------------------------------

echo
echo "an SDK on PATH is reported as present, without probing the network"
new_env
stub_muxer "$SANDBOX/bin/dotnet" "10.0.203"
stub_curl 0
out="$(run_hook)"
assert_equals "$(printf '%s' "$out" | json_field hookSpecificOutput.hookEventName)" \
  "SessionStart" "emits a SessionStart hook payload"
ctx="$(printf '%s' "$out" | json_field hookSpecificOutput.additionalContext)"
assert_contains "$ctx" "PRESENT" "verdict is PRESENT"
assert_contains "$ctx" "10.0.203" "names the SDK version it measured"
assert_contains "$ctx" "full solution suite is runnable" "says the full-solution rule applies"
if [ -f "$SANDBOX/curl.log" ]; then
  fail "no network when an SDK is already usable" "curl was invoked"
else
  pass "no network when an SDK is already usable"
fi
drop_env

echo
echo "an SDK below the floor is not accepted as usable"
new_env
stub_muxer "$SANDBOX/bin/dotnet" "6.0.400"
stub_curl 6
ctx="$(hook_context)"
assert_contains "$ctx" "UNAVAILABLE" "an SDK 6 muxer does not satisfy a net8+ repo"
drop_env

echo
echo "a muxer only under \$HOME/.dotnet is found, and its path is handed to the agent"
new_env
stub_muxer "$SANDBOX/home/.dotnet/dotnet" "10.0.203"
stub_curl 0
ctx="$(hook_context)"
assert_contains "$ctx" "PRESENT" "verdict is PRESENT"
assert_contains "$ctx" "$SANDBOX/home/.dotnet/dotnet" "names the muxer path, since PATH will not find it"
drop_env

echo
echo "no SDK and no egress is UNAVAILABLE, and says exactly what cannot be run"
new_env
stub_curl 6
ctx="$(hook_context)"
assert_contains "$ctx" "UNAVAILABLE" "verdict is UNAVAILABLE"
assert_contains "$ctx" "egress-blocked" "names the reason it measured"
assert_contains "$ctx" "Motiv.Tests" "names the unit suite"
assert_contains "$ctx" "src/examples/" "names the example suites"
assert_contains "$ctx" "pnpm e2e" "names the e2e suite"
assert_contains "$ctx" "Motiv.Studio" "names the app that cannot be launched"
assert_contains "$ctx" "ui/" "names what IS runnable"
assert_contains "$ctx" "say which suites you could not run" "restates the disclosure obligation"
drop_env

echo
echo "a recorded failure is not re-probed on the next session"
new_env
stub_curl 6
run_hook >/dev/null
first="$(curl_calls)"
ctx="$(hook_context)"
assert_equals "$(curl_calls)" "$first" "the second session does not re-download"
assert_contains "$ctx" "UNAVAILABLE" "the recorded verdict is replayed"
# A replay whose reason is "because last time" is worth nothing: the reason is what tells a later
# reader whether the network policy has since changed, so it has to survive the round trip.
assert_contains "$ctx" "egress-blocked" "the replay carries the reason that was measured, not a placeholder"
drop_env

echo
echo "MOTIV_DOTNET_HOOK_FORCE=1 re-probes despite the record"
new_env
stub_curl 6
run_hook >/dev/null
first="$(curl_calls)"
MOTIV_DOTNET_HOOK_FORCE=1 run_hook >/dev/null
if [ "$(curl_calls)" -gt "$first" ]; then
  pass "forcing re-probes"
else
  fail "forcing re-probes" "curl not re-invoked"
fi
drop_env

echo
echo "a successful provision installs the SDK and both test-time runtimes"
new_env
stub_curl 0
ctx="$(hook_context)"
assert_contains "$ctx" "PROVISIONED" "verdict is PROVISIONED"
log="$(cat "$SANDBOX/install.log")"
assert_contains "$log" "--channel 10.0" "installs the SDK channel the repo builds with"
assert_contains "$log" "--channel 8.0 --runtime dotnet" "installs the net8.0 runtime the tests need"
assert_contains "$log" "--channel 9.0 --runtime dotnet" "installs the net9.0 runtime the tests need"
assert_contains "$ctx" "$SANDBOX/home/.dotnet/dotnet" "hands over the muxer path"
assert_not_contains "$ctx" "--framework net10.0" "no partial-install caveat when nothing failed"
drop_env

echo
echo "an SDK that installs without its runtimes is reported as partial, not as whole"
new_env
stub_curl 0
printf '8.0\n9.0\n' >"$SANDBOX/install-fail"
ctx="$(hook_context)"
assert_contains "$ctx" "PROVISIONED" "the SDK did install, so the verdict is PROVISIONED"
assert_contains "$ctx" "8.0" "names the runtime channel that failed"
assert_contains "$ctx" "--framework net10.0" "gives the runnable subset rather than implying all of it"
drop_env

echo
echo "an SDK channel that fails to install is UNAVAILABLE, not PROVISIONED"
new_env
stub_curl 0
printf '10.0\n' >"$SANDBOX/install-fail"
ctx="$(hook_context)"
assert_contains "$ctx" "UNAVAILABLE" "verdict is UNAVAILABLE"
assert_contains "$ctx" "install-failed" "distinguishes a failed install from blocked egress"
drop_env

echo
echo "an installer that exits 0 having installed nothing is UNAVAILABLE, not PROVISIONED"
# The gap this case exists for: exit 0 from the installer is not evidence an SDK is there, so the
# hook re-resolves the muxer afterwards. Without this case that re-resolution can be deleted and the
# suite stays green while the hook reports PROVISIONED with an empty version.
new_env
stub_curl 0
printf '10.0\n8.0\n9.0\n' >"$SANDBOX/install-noop"
ctx="$(hook_context)"
assert_contains "$ctx" "UNAVAILABLE" "exit 0 is not taken as proof of an SDK"
assert_contains "$ctx" "install-failed" "reported as a failed install, not as blocked egress"
assert_not_contains "$ctx" "PROVISIONED" "does not claim a provision it did not verify"
drop_env

echo
echo "a usable dotnet further along PATH does not leak into the verdict"
# The regression case for the CI failure above, asserted directly rather than only as a side effect
# of every other case's fabrication.
new_env
out="$(run_hook)"
ctx="$(printf '%s' "$out" | json_field hookSpecificOutput.additionalContext)"
assert_contains "$ctx" "UNAVAILABLE" "the shadowed host muxer is not resolved"
assert_not_contains "$ctx" "10.0.999" "the host SDK version does not appear in the verdict"
drop_env

echo
echo "MOTIV_DOTNET_HOOK_SKIP=1 suppresses the hook entirely"
new_env
stub_curl 6
out="$(MOTIV_DOTNET_HOOK_SKIP=1 run_hook)"
assert_equals "$out" "" "emits nothing"
drop_env

echo
echo "an unset HOME does not abort the hook"
# `set -u` plus a bare $HOME in a default expansion aborts before anything is emitted, which breaks
# the one guarantee the hook makes. The existing "never fails" case did not catch it: it removed the
# state dir and pointed STATE_DIR at an unwritable path, but HOME was still set, and HOME is expanded
# for the *default* of STATE_DIR — so overriding STATE_DIR is exactly what hides this.
new_env
out="$(env -u HOME -u XDG_STATE_HOME -u MOTIV_DOTNET_HOOK_STATE_DIR -u MOTIV_DOTNET_HOOK_INSTALL_ROOT \
  PATH="$PATH" bash "$HOOK" </dev/null 2>"$SANDBOX/stderr")"
assert_equals "$?" "0" "exits 0 with no HOME in the environment"
ctx="$(printf '%s' "$out" | json_field hookSpecificOutput.additionalContext)"
assert_contains "$ctx" "UNAVAILABLE" "still reports a verdict rather than emitting nothing"
drop_env

echo
echo "the hook never fails the session"
new_env
# No curl, no dotnet, no state dir writability — the worst case available.
rm -rf "$SANDBOX/state"
export MOTIV_DOTNET_HOOK_STATE_DIR=/proc/nonexistent/state
run_hook >/dev/null
assert_equals "$?" "0" "exits 0 even when everything it depends on is missing"
drop_env

echo
printf '\n%s passed, %s failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
