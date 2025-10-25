# Agent usage and commands

This document explains how maintainers and contributors can instruct the GitHub Copilot coding agent in this repository.

Supported instruction channels
- PR front-matter (YAML at top of PR body) — preferred for reproducibility.
- PR comment using slash-style commands (e.g. `/copilot run apply-fixes`).
- Add a label that triggers a run (e.g. `copilot: run`).

Example PR front-matter (place at the top of the PR body):

```yaml
copilot:
  run: "apply-fixes"
  target_branch: "master"
  auto_merge: false
  run_tests: true
  required_approvals: 1
```

Example slash command via PR comment:
- `/copilot run apply-fixes --target=master --run-tests`

Recommended labels
- `copilot: run`       -> instructs agent to run its default task on the PR
- `copilot: approve`   -> if allowed by policy, agent may merge once checks pass

How to enable and grant permissions
1. Merge `.github/agents/copilot-agent.yml` into master.
2. As a repository administrator, install/authorize the GitHub Copilot coding agent app and grant it repository permissions that match the manifest (Contents: write, Pull requests: write, Checks: write, Actions: write/read, Issues: write).
3. Ensure Actions is enabled for the repository and branch protection rules are compatible with the manifest (or allow the agent to have the bypass when appropriate).

Safety & governance
- Keep allow paths narrow — only grant the agent write access where it needs it.
- Prefer `require_review_before_merge: true` during initial rollout.
- Use audit logs to review agent activity and require a human reviewer until you trust the automation.

PR details
- Branch name: copilot-agent-config-and-docs
- Changes: add/modify .github/agents/copilot-agent.yml and add AGENTS.md at repo root
- This PR is intentionally limited to configuration and documentation; it does not add any workflows that push changes or perform merges.

If the repository settings or installed apps block the agent from running, include a clear note in the PR description describing actions an admin must take: enable Actions, install Copilot coding agent app, grant repo write permissions to agent, or run onboarding steps.

Author: GitHub Copilot (@copilot) acting on behalf of adamhathcock.
