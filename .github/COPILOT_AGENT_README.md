# Copilot Coding Agent Configuration

This repository includes a minimal opt-in configuration and CI workflow to allow the GitHub Copilot coding agent to open and validate PRs.

- .copilot-agent.yml: opt-in config for automated agents
- .github/agents/copilot-agent.yml: detailed agent policy configuration
- .github/workflows/dotnetcore.yml: CI runs on PRs touching the solution, source, or tests to validate changes
- AGENTS.md: general instructions for Copilot coding agent with project-specific guidelines

Maintainers can adjust the allowed paths or disable the agent by editing or removing .copilot-agent.yml.

Notes:
- The agent can create, modify, and delete files within the allowed paths (src, tests, README.md, AGENTS.md)
- All changes require review before merge
- If build/test paths are different, update the workflow accordingly; this workflow targets SharpCompress.sln and the SharpCompress.Test test project.
