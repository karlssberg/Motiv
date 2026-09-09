#!/usr/bin/env bash
# SessionStart hook: establish, by measurement, whether this session can build and test the .NET
# side of Motiv — and if it cannot, say so loudly enough that the session cannot mistake silence
# for capability.
#
# Why this exists (issue #173). Claude Code cloud containers for this repo ship no .NET SDK, and
# egress to the .NET distribution is blocked, so `Motiv.Tests`, the `src/examples/*.Tests` suites,
# `pnpm e2e` and `Motiv.Studio` are all unrunnable there. CLAUDE.md's standing instruction to run the
# full solution suite is then unfollowable, and the failure mode is silent: the agent runs the `ui/`
# workspace, sees green, and reports it as though it were the whole.
#
# CLAUDE.md used to carry that as a flat assertion — "there is no .NET SDK". That is a claim which
# goes stale the moment a container gains one, and a session reading it would then decline work it
# could actually do. So the capability is *probed* at session start and the answer reported here;
# CLAUDE.md points at this verdict rather than restating one.
#
# Three verdicts:
#   PRESENT      a usable SDK was found. The full-solution rule applies as written.
#   PROVISIONED  none was found, and one was installed just now. May be partial — see the caveat.
#   UNAVAILABLE  none was found and none could be installed. The disclosure obligation applies.
#
# The probe is cheap in the blocked case, which is the common one: fetching the ~70 KB install script
# is itself the egress test, so a blocked container spends `MOTIV_DOTNET_HOOK_TIMEOUT` seconds and
# stops. Only a container that *can* reach the distribution pays for a full install, and only once —
# the outcome is recorded and replayed on later sessions. `settings.json` gives this hook a long
# timeout for that reason.
#
# The hook never fails a session: every path exits 0.
#
# Knobs (all optional, all used by scripts/agent/tests/dotnet-capability.test.sh):
#   MOTIV_DOTNET_HOOK_SKIP=1          emit nothing and exit
#   MOTIV_DOTNET_HOOK_FORCE=1         re-probe even if a previous failure was recorded
#   MOTIV_DOTNET_HOOK_STATE_DIR       where the recorded outcome lives
#   MOTIV_DOTNET_HOOK_INSTALL_ROOT    where an SDK is installed to
#   MOTIV_DOTNET_HOOK_TIMEOUT         seconds allowed for the install-script fetch

set -uo pipefail

[ "${MOTIV_DOTNET_HOOK_SKIP:-}" = "1" ] && exit 0

# The repo builds with an SDK of at least this major version; anything older cannot restore it.
readonly SDK_FLOOR=8
# The SDK channel the solution builds with, and the runtime channels its tests execute on. `dotnet
# test` runs Motiv.Tests on net8.0, net9.0 and net10.0, so an SDK alone leaves two thirds of the
# matrix unrunnable — which is why the runtimes are installed too, and why their absence downgrades
# the verdict's detail rather than passing unmentioned.
readonly SDK_CHANNEL=10.0
readonly RUNTIME_CHANNELS="8.0 9.0"
readonly INSTALL_SCRIPT_URL=https://dot.net/v1/dotnet-install.sh

# $HOME is expanded for the two defaults below, and under `set -u` an unset HOME aborts the script
# before it can emit anything — breaking the one guarantee this hook makes, in the environments least
# likely to have a .NET SDK. There is no durable per-user location without a HOME, so a scratch dir
# stands in: the verdict is what matters, and a marker that does not outlive the container is still
# better than no verdict at all.
HOME_DIR=${HOME:-${TMPDIR:-/tmp}}
STATE_DIR=${MOTIV_DOTNET_HOOK_STATE_DIR:-${XDG_STATE_HOME:-$HOME_DIR/.local/state}/motiv}
INSTALL_ROOT=${MOTIV_DOTNET_HOOK_INSTALL_ROOT:-$HOME_DIR/.dotnet}
TIMEOUT=${MOTIV_DOTNET_HOOK_TIMEOUT:-8}
RECORD="$STATE_DIR/dotnet-capability"

# --- reporting ----------------------------------------------------------------------------------

# Emits the accumulated lines as a SessionStart payload. Escaping is done here rather than by
# shelling out to a JSON library: a hook that needs python3 on PATH is a hook that can fail to report
# that something else is missing.
emit() {
  local json="" line
  for line in "$@"; do
    line=${line//\\/\\\\}
    line=${line//\"/\\\"}
    if [ -z "$json" ]; then json=$line; else json="$json\\n$line"; fi
  done
  printf '{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"%s"}}\n' "$json"
}

readonly FULL_SUITE="The full solution suite is runnable; CLAUDE.md's full-solution rule applies as written."
readonly UNRUNNABLE="Unrunnable in this session: Motiv.Tests, the src/examples/*.Tests suites (Poker, ECommerce, SmartHome), \`pnpm e2e\` (it drives the .NET host), and the studio / rule-authoring-blazor launch configurations, which need Motiv.Studio to build."
readonly RUNNABLE="Runnable in this session: the ui/ pnpm workspace only."
readonly OBLIGATION="CLAUDE.md requires the full solution suite for any change to justification output, assertion text or result formatting. You cannot follow that instruction here. Do not report the ui/ suite green as if it were the whole — say which suites you could not run and why. A slice touching C# needs a local session."

report_unavailable() {
  emit \
    ".NET capability: UNAVAILABLE — no usable SDK, and one could not be provisioned ($1)." \
    "$UNRUNNABLE" \
    "$RUNNABLE" \
    "$OBLIGATION"
  exit 0
}

# --- resolution ---------------------------------------------------------------------------------

# Echoes the newest SDK version a muxer reports, or nothing if it reports none at or above the floor.
# `--list-sdks` prints "<version> [<path>]" per line in ascending version order, so the last line at
# or above the floor is the newest; a muxer with only runtimes installed prints nothing at all, which
# is exactly the partial state this script can leave behind.
usable_sdk_version() {
  local muxer=$1 line version major best=
  while IFS= read -r line; do
    version=${line%% *}
    major=${version%%.*}
    case "$major" in
      ''|*[!0-9]*) continue ;;
    esac
    [ "$major" -ge "$SDK_FLOOR" ] && best=$version
  done < <("$muxer" --list-sdks 2>/dev/null)
  [ -n "$best" ] && printf '%s' "$best"
}

# Sets MUXER and SDK_VERSION to the first muxer that satisfies the floor; returns 1 if none does.
#
# PATH is searched first, then DOTNET_ROOT, then the install root. The last two matter because a
# muxer under $HOME/.dotnet is invisible to a plain `dotnet` invocation, and exporting DOTNET_ROOT
# does not make it visible — the muxer path itself has to be used. That is why the verdict always
# names the path it found rather than just saying "an SDK is present".
MUXER=
SDK_VERSION=
resolve_muxer() {
  local candidate version
  for candidate in "$(command -v dotnet 2>/dev/null)" "${DOTNET_ROOT:-}/dotnet" "$INSTALL_ROOT/dotnet"; do
    # `[ -x "" ]` is false, so this covers the empty candidate `command -v` yields when there is
    # no dotnet on PATH, and the "$DOTNET_ROOT/dotnet" that degenerates to "/dotnet" when unset.
    [ -x "$candidate" ] || continue
    version=$(usable_sdk_version "$candidate")
    if [ -n "$version" ]; then
      MUXER=$candidate
      SDK_VERSION=$version
      return 0
    fi
  done
  return 1
}

# --- 1. already usable? -------------------------------------------------------------------------

if resolve_muxer; then
  emit ".NET capability: PRESENT — SDK $SDK_VERSION at $MUXER. $FULL_SUITE"
  exit 0
fi

# --- 2. has a previous session already established that it cannot be? ---------------------------

if [ "${MOTIV_DOTNET_HOOK_FORCE:-}" != "1" ] && [ -r "$RECORD" ]; then
  report_unavailable "$(cat "$RECORD" 2>/dev/null)"
fi

record_failure() {
  mkdir -p "$STATE_DIR" 2>/dev/null && printf '%s' "$1" >"$RECORD" 2>/dev/null
  report_unavailable "$1"
}

# --- 3. provision ---------------------------------------------------------------------------------

workdir=$(mktemp -d "${TMPDIR:-/tmp}/motiv-dotnet-install.XXXXXX" 2>/dev/null) \
  || record_failure "no-writable-tempdir"
installer="$workdir/dotnet-install.sh"
trap 'rm -rf "$workdir"' EXIT

# Fetching the installer is the egress probe. `--max-time` rather than a `timeout` wrapper: macOS
# has no `timeout`, and a wrapper that silently does not exist would run the fetch unbounded.
if ! curl -fsSL --max-time "$TIMEOUT" -o "$installer" "$INSTALL_SCRIPT_URL" 2>/dev/null; then
  record_failure "egress-blocked: $INSTALL_SCRIPT_URL unreachable within ${TIMEOUT}s"
fi

bash "$installer" --channel "$SDK_CHANNEL" --install-dir "$INSTALL_ROOT" --no-path >/dev/null 2>&1 \
  || record_failure "install-failed: SDK channel $SDK_CHANNEL"

missing=
for channel in $RUNTIME_CHANNELS; do
  bash "$installer" --channel "$channel" --runtime dotnet --install-dir "$INSTALL_ROOT" --no-path \
    >/dev/null 2>&1 || missing="${missing:+$missing }$channel"
done

resolve_muxer || record_failure "install-failed: no SDK at or above $SDK_FLOOR after installing channel $SDK_CHANNEL"

# The hook's environment does not outlive the hook, so PATH cannot be handed over — the muxer path
# can, and must be, or the session will conclude from `dotnet: command not found` that step 1 lied.
lines=(
  ".NET capability: PROVISIONED — SDK $SDK_VERSION installed at $INSTALL_ROOT."
  "The muxer is not on PATH. Invoke it as $MUXER (or export PATH=\"$INSTALL_ROOT:\$PATH\" in each shell); setting DOTNET_ROOT alone is not enough."
)
if [ -n "$missing" ]; then
  lines+=("PARTIAL: the .NET runtime channels $missing failed to install, so \`dotnet test\` will fail on the corresponding target frameworks. The runnable subset is \`$MUXER test --framework net10.0\`; say so rather than reporting the suite green as if the whole matrix had run.")
else
  lines+=("$FULL_SUITE")
fi
emit "${lines[@]}"
exit 0
