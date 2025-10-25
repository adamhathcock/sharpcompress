# Copilot Coding Agent Configuration

This repository includes a minimal opt-in configuration and CI workflow to allow the GitHub Copilot coding agent to open and validate PRs.

- .copilot-agent.yml: opt-in config for automated agents
- .github/workflows/dotnetcore.yml: CI runs on PRs touching the solution, source, or tests to validate changes
- AGENTS.yml: general information for this project

Maintainers can adjust the allowed paths or disable the agent by editing or removing .copilot-agent.yml.

Notes:
- Do not change any other files in the repository.
- If build/test paths are different, update the workflow accordingly; this workflow targets SharpCompress.sln and the SharpCompress.Tests test project.
