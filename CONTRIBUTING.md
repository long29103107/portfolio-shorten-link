# Contributing

## Git Commit Convention

Every commit message must use this format:

```text
<type>(<scope>): <short description>
```

### Types

| Type | Use for |
|---|---|
| `feat` | New functionality |
| `fix` | Bug fixes |
| `refactor` | Code changes that neither add a feature nor fix a bug |
| `test` | Adding or updating tests |
| `docs` | Documentation-only changes |
| `chore` | Maintenance work that does not change product behavior |
| `build` | Build system or dependency changes |
| `ci` | CI workflow changes |
| `perf` | Performance improvements |
| `style` | Formatting-only changes |

### Scope

The scope is required. Use a short, lowercase, kebab-case name for the affected
area, such as `api`, `web`, `audit`, `hosting`, `repository`, `security`, or
`release`.

### Description

- Write the description in English.
- Use the imperative mood, for example `add`, `fix`, or `update`.
- Start with a lowercase letter.
- Do not end with a period.
- Keep the complete subject line at 72 characters or fewer when practical.

### Examples

```text
feat(audit): add audit log filtering
fix(hosting): prevent duplicate endpoint registration
refactor(repository): use lean query projections
test(short-links): cover expired link resolution
docs(release): document NuGet publishing workflow
```

Commits that do not follow this convention should be rewritten before they are
merged into `main`.
