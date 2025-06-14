# Copilot Instructions

These instructions are for GitHub Copilot and AI agents working on this repository. They summarize the most important project rules and workflow requirements.

## Source of Truth
- Always use `project_fact_sheet.md` and `project_instructions.md` as the primary context for all tasks.

## Workflow Requirements
- At the start of every project or major feature, before implementation, collaboratively write or update a requirements document that:
  - Loosely follows the template in requirements.1.md and requirements.2.md
  - Includes a Kanban-style task list (in a separate section)
  - Includes high-level and technical design sections
  - Includes implementation context and codebase helpers (gathered by inspecting the codebase for relevant files, DTOs, and code snippets)
  - Includes implementation hints
  - Includes a Glossary section at the bottom, defining key domain and project-specific terms.
- Inspect the codebase for relevant files and code snippets to aid implementation, and reference these in the requirements document.
- For all tasks, write or update Gherkin-style scenarios or xUnit tests to prove the feature works, unless the user explicitly states otherwise.
- At the end of each project, generate an Architecture Decision Record (ADR) summarizing key decisions using the MADR template. Store ADRs in a `decisions` folder with a running `README.md` linking to each ADR.
- Proactively suggest updates to the fact sheet and instructions when changes affect project purpose, user stories, architecture, tech stack, or cross-cutting rules.

## Coding Standards
- Follow .NET 8 and C# best practices for code style, structure, and naming.
- Prefer using the `Result`/`IResult` pattern (see `Stocks.Shared.Result`, `Stocks.Shared.IResult`, and the `Results.cs` file) for error handling instead of throwing exceptions, unless an exception is truly exceptional or unrecoverable.
- Keep all documentation, including ADRs, Kanban task lists, and test scenarios, clear, concise, and up to date.

---

This file should be kept in sync with `project_fact_sheet.md` and `project_instructions.md`.