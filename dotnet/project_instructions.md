# Project Instructions

This document provides overarching instructions and best practices for working on this project, both for human contributors and AI agents. It is intended to ensure consistency, maintainability, and alignment with project goals.

## General Principles
- Always use `project_fact_sheet.md` as the source of truth for project purpose, user stories, architecture, tech stack, and cross-cutting rules.
- Keep `project_fact_sheet.md` up to date. Whenever you implement, refactor, or remove a significant feature, update the fact sheet to reflect the current state of the project.
- Prioritize clarity, maintainability, and extensibility in all code and documentation.

## Workflow Guidelines
- Before starting a new feature or major change, review the fact sheet and this instructions document.
- Before beginning implementation of any feature or project, produce a Kanban-style list of tasks, each sized at 2 hours or less, as part of the planning process.
- At the end of each project, summarize key architectural decisions in an Architecture Decision Record (ADR) using the MADR template.
- After completing a feature, bugfix, or architectural change, review and update the fact sheet as needed.
- Document any new cross-cutting rules, tech stack changes, or architectural decisions in the fact sheet.
- If you are unsure whether a change warrants an update, err on the side of updating the fact sheet.
- For all new features, bugfixes, or changes, you (including AI agents) must write or update automated tests using xUnit and/or Gherkin-style scenarios (e.g., SpecFlow) that prove the feature works as intended. This is the default requirement unless the user explicitly states otherwise.
- Ensure that new functionality is covered by tests that prove requirements are met.
- Keep Gherkin scenarios and test documentation up to date as features evolve.

## Coding and Documentation Standards
- Follow .NET 8 and C# best practices for code style, structure, and naming.
- Use clear, descriptive commit messages and pull request descriptions.
- Write concise, meaningful comments where necessary, but prefer self-explanatory code.
- Keep documentation (including this file and the fact sheet) clear, concise, and up to date.
- Write xUnit tests for new code and maintain existing tests. Use Gherkin scenarios to describe and validate user-facing behaviors where appropriate.

## AI Agent Instructions
- Always use `project_fact_sheet.md` and this instructions file as primary context for all tasks.
- Proactively suggest updates to the fact sheet when you detect changes that affect project purpose, user stories, architecture, tech stack, or cross-cutting rules.
- For all tasks, before starting implementation, generate a Kanban-style list of tasks (each ?2h) as part of planning, unless the user explicitly specifies otherwise.
- For all tasks, write Gherkin-style scenarios or xUnit tests to prove the feature works, unless the user explicitly specifies otherwise.
- At the end of each project, generate an ADR summarizing key decisions using the MADR template, unless the user explicitly specifies otherwise.
- When in doubt, ask the user if the fact sheet or instructions should be updated.

## Collaboration
- Communicate any major changes, questions, or uncertainties to the project owner.
- Encourage feedback and continuous improvement of both code and documentation.

---

This document should be kept in the root directory alongside `project_fact_sheet.md` and referenced at the start of every new session or major task.
