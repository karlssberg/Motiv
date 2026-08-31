# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically when run inside a clone.

## When `gh` is unavailable

`gh` is not installed in Claude Code cloud containers, so every command above fails there. The same
operations are available through the **GitHub MCP tools**; use them whenever `which gh` comes back
empty. Infer `owner`/`repo` from `git remote -v` — the MCP tools take them as explicit arguments.

| Operation | `gh` | MCP |
|---|---|---|
| Create an issue | `gh issue create` | `issue_write` (`method: create`) |
| Read an issue | `gh issue view` | `issue_read` (`get`, `get_comments`, `get_labels`) |
| List issues | `gh issue list` | `list_issues` (`state`, `labels`, `fields`) |
| Comment | `gh issue comment` | `add_issue_comment` |
| Label | `gh issue edit --add-label` | `issue_write` (`method: update`, `labels`) |
| Assign / claim | `gh issue edit --add-assignee @me` | `get_me`, then `issue_write` (`method: update`, `assignees`) |
| Close | `gh issue close` | `issue_write` (`method: update`, `state: closed`, `state_reason`) |
| Read a PR | `gh pr view` / `gh pr diff` | `pull_request_read` (`get`, `get_diff`, `get_comments`) |
| Create a child ticket | `gh issue create` + `gh api` sub-issues endpoint | `issue_write` (`method: create`, `parent_issue_number`) — creates and attaches in one call |
| Attach an existing issue as a child | `gh api` sub-issues endpoint | `sub_issue_write` (`method: add`, `sub_issue_id` — the **database id**, not the number) |
| List sub-issues | `gh api` sub-issues endpoint | `issue_read` (`get_sub_issues`) |

**One operation has no MCP equivalent: native issue dependencies.** There is no MCP tool for the
`dependencies/blocked_by` endpoint, and `issue_dependencies_summary` is not returned. In a cloud
session, use the documented fallback instead — a `Blocked by: #<n>, #<n>` line at the top of the
child body — and treat a ticket as unblocked when every issue it names is closed. A dependency added
this way is still readable by a local session that does have `gh`; it is just not visible in the
GitHub UI as an edge.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> --comments` and `gh pr diff <number>` for the diff.
- **List external PRs for triage**: `gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments` then keep only `authorAssociation` of `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, or `NONE` (drop `OWNER`/`MEMBER`/`COLLABORATOR`).
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either — resolve with `gh pr view 42` and fall back to `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. `gh issue create --label wayfinder:map`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue (`gh api` on the sub-issues endpoint). Where sub-issues aren't enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of the child body. Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is assigned to the driving dev.
- **Blocking**: GitHub's **native issue dependencies** — the canonical, UI-visible representation. Add an edge with `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where `<blocker-db-id>` is the blocker's numeric **database id** (`gh api repos/<owner>/<repo>/issues/<n> --jq .id`, _not_ the `#number` or `node_id`). GitHub reports `issue_dependencies_summary.blocked_by` (open blockers only — the live gate). Where dependencies aren't available, fall back to a `Blocked by: #<n>, #<n>` line at the top of the child body. A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`gh issue list --state open`, scoped to the map's sub-issues / task list), drop any with an open blocker (`issue_dependencies_summary.blocked_by > 0`, or an open issue in the `Blocked by` line) or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me` — the session's first write.
- **Resolve**: `gh issue comment <n> --body "<answer>"`, then `gh issue close <n>`, then append a context pointer (gist + link) to the map's Decisions-so-far.
