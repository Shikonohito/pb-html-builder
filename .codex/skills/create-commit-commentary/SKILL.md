---
name: create-commit-commentary
description: Compose commit messages in one consistent Conventional Commits style. Use when Codex needs to draft, refine, review, or explain a Git commit message/comment for staged changes, unstaged diffs, supplied change summaries, or release-related commit text; this skill only handles commit message wording and formatting, not staging, committing, pushing, or changing files.
---

# Create Commit Commentary

## Scope

Draft exactly one commit message unless the user explicitly asks for options.

Do not stage files, create commits, amend commits, push branches, edit project files, or perform unrelated review work as part of this skill. If more context is needed, inspect only the relevant Git diff or ask for the change summary.

## Style

Use Conventional Commits 1.0.0:

```text
<type>(<scope>): <subject>

<body>

<footer>
```

Prefer this exact project-neutral style:

- Use English for the message text unless the user explicitly asks for another language.
- Keep the header to 72 characters or less when practical.
- Use lowercase `type`.
- Use a concise noun scope only when it clarifies the affected area.
- Write the subject in imperative mood, with no trailing period.
- Include a body only when the header cannot explain the intent or impact.
- Wrap body lines at about 72 characters.
- Use footer lines only for `BREAKING CHANGE:` or issue references.
- Do not use emoji, markdown formatting, prefixes outside Conventional Commits, or generated-by/co-authored trailers unless the user asks.

## Type Selection

Choose the narrowest accurate type:

- `feat`: user-visible new capability or behavior.
- `fix`: bug fix or behavioral correction.
- `perf`: performance improvement without behavior change.
- `refactor`: internal restructuring without feature or bug-fix semantics.
- `docs`: documentation-only change.
- `test`: tests-only change or test infrastructure.
- `build`: build system, dependencies, packaging, project files.
- `ci`: CI/CD workflows and automation.
- `style`: formatting-only change with no code behavior impact.
- `chore`: maintenance that does not fit the other types.
- `revert`: revert a previous commit.

Use `!` before the colon for breaking changes:

```text
feat(api)!: require explicit tenant ids

BREAKING CHANGE: API calls must now pass tenant ids explicitly.
```

## Workflow

1. Gather context from the user's summary or the relevant Git diff.
2. Identify the primary intent of the change, not every touched file.
3. Pick one type and optional scope.
4. Draft one clean message in the standard format.
5. If the diff mixes unrelated changes, either choose the dominant intent and mention the secondary impact in the body, or tell the user the changes should be split.

## Output

When the user asks only for a commit message, output just the message in a fenced `text` block.

When reviewing an existing commit message, state whether it fits the style, then provide the corrected message in a fenced `text` block.
