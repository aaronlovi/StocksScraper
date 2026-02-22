# CLAUDE.md — Angular Frontend

## Build, Test & Serve

```bash
# Build
npx ng build

# Run all tests (Vitest)
npx ng test --watch=false

# Run a single test file
npx ng test --watch=false --include 'src/app/features/scoring/scoring.component.spec.ts'

# Dev server
npx ng serve
```

## Project Structure

```
src/app/
├── core/              # Singletons: layout components, services
│   ├── layout/        # sidebar, titlebar
│   └── services/      # api.service (HTTP + all DTOs/interfaces)
├── features/          # Route-level feature modules (one per page)
├── shared/
│   ├── components/    # Reusable UI components (breadcrumb, sparkline-chart, etc.)
│   ├── styles/        # Shared CSS imported by multiple components
│   └── *.utils.ts     # Pure utility functions (format, sort, sparkline, returns-summary)
└── styles.scss        # Global styles (base table, links, form inputs, utility classes)
```

- **Angular 21** with standalone components (no NgModules).
- **Vitest** for unit tests via `@angular/build:unit-test`.

## Coding Standards

### File Organization

- **Separate files for template, styles, and TypeScript.** Every component must use `templateUrl` and `styleUrls` — never inline `template:` or `styles:` in the `@Component` decorator.
- **One component per directory** with the naming convention: `name.component.ts`, `name.component.html`, `name.component.css`, `name.component.spec.ts`.

### Component Architecture

- **Extract shared UI into reusable components** under `shared/components/`. If a UI pattern appears in two or more features, it should be a shared component with `@Input()`/`@Output()` bindings.
- **Feature components** live under `features/<feature-name>/` and correspond to a route.
- **Core singletons** (services, layout shell) live under `core/`.

### Styling

- **Prefer global styles** in `src/styles.scss` for base element rules (tables, links, form inputs) and utility classes (`.num`, `.error`, `.positive`, `.negative`).
- **Shared CSS files** in `shared/styles/` for patterns used across multiple components (e.g., `info-tooltip.css`, `report-table.css`). Import these via `styleUrls` alongside the component's own CSS.
- **Component-scoped CSS** (`.component.css`) only for styles unique to that component.
- Avoid duplicating the same CSS rule in multiple component stylesheets — move it to `shared/styles/` or `styles.scss` instead.

### TypeScript Conventions

- All components are **standalone** — explicitly import dependencies in the `imports` array.
- Use **signals** (`signal()`, `computed()`) for reactive state, not RxJS subjects for UI state.
- Use the formatting utilities in `shared/format.utils.ts` (`fmtCurrency`, `fmtPct`, `fmtRatio`, `fmtPrice`, `formatAbbrev`) rather than writing ad-hoc formatting.
- **No LINQ-style chaining in production code** — use explicit `for` loops. LINQ-style is acceptable in tests.
- **Explicit imports** — do not rely on implicit/auto-imports.

### Testing

- Every new component should have a `.spec.ts` file.
- Use Angular `TestBed` with `provideHttpClientTesting()` for service mocks.
