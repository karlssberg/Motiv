.PHONY: studio hooks-lint hooks-test

studio:
	ASPNETCORE_ENVIRONMENT=Development ./run-studio.sh

# The agent-environment scripts. `dotnet-capability.sh` is the SessionStart probe that tells an agent
# session whether the .NET suites are runnable at all (issue #173). Neither target needs a .NET SDK —
# the tests fabricate their own PATH. .github/workflows/agent-env.yml invokes these same two targets
# rather than restating the commands, so a local run and CI cannot drift apart.
hooks-lint:
	shellcheck --shell=bash scripts/agent/*.sh scripts/agent/tests/*.sh

hooks-test:
	bash scripts/agent/tests/dotnet-capability.test.sh
