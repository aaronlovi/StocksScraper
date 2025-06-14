# Copilot Instructions

These instructions are for GitHub Copilot and AI agents working on this repository. They summarize the most important project rules and workflow requirements.

## Source of Truth
- Always use `project_fact_sheet.md` and `project_instructions.md` as the primary context for all tasks.

## Workflow Requirements
- Before implementation, break down all features and projects into a Kanban-style list of tasks, each estimated at 2 hours or less.
- For all tasks, write or update Gherkin-style scenarios or xUnit tests to prove the feature works, unless the user explicitly states otherwise.
- At the end of each project, generate an Architecture Decision Record (ADR) summarizing key decisions using the MADR template.
- Proactively suggest updates to the fact sheet and instructions when changes affect project purpose, user stories, architecture, tech stack, or cross-cutting rules.

## Coding Standards
- Follow .NET 8 and C# best practices for code style, structure, and naming.
- Keep all documentation, including ADRs, Kanban task lists, and test scenarios, clear, concise, and up to date.

---

This file should be kept in sync with `project_fact_sheet.md` and `project_instructions.md`.