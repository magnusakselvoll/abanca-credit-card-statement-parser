# CLAUDE.md

Instructions for Claude Code when working on this repository.

## Issue Tracking

Issues are tracked in GitHub. Use `gh issue list` to see open issues and `gh issue view <number>` for details.

## Git Workflow (GitHub Flow)

Always use GitHub Flow when working on issues:

1. **Create a feature branch** before making any file edits — no exceptions:
   - First fetch and checkout latest main: `git fetch origin && git checkout main && git pull`
   - Branch name format: `<issue-number>-<short-description>` (e.g., `2-improve-parsing`)
   - Create and checkout the branch: `git checkout -b 2-improve-parsing`
   - **Do not read or edit any files until the branch is created.** This prevents accidentally committing to main (direct pushes to main are blocked).
   - **Only use worktrees** when explicitly asked (e.g., "use a worktree", "work on several issues in parallel")

2. **Commit** changes with descriptive messages:
   - Write commit messages as plain double-quoted strings — no heredocs, no `$()` substitution
   - Each `-m` value must be a single line — newlines inside a `-m` string cause a "quoted characters in flag names" error
   - For multi-line messages use separate `-m` flags, one per line: `git commit -m "title" -m "body line"`

3. **Push** the branch and **create a PR**:
   - **Ask before creating the PR** - the user may have feedback based on the console output or code
   - PR title should be descriptive of the change
   - Reference the issue in the PR body with `Closes #<issue-number>` to auto-close on merge
   - Pass `--title` and `--body` as plain strings to `gh pr create` — no heredocs, no command substitution, and no backticks (backticks in strings trigger a command substitution approval prompt even when used as markdown formatting)
   - Always pass `--head <branch-name> --base main` to `gh pr create` — without these, `gh` picks up the main repo context and fails with "head branch is the same as base branch"

4. **Merge** after review (squash merge preferred for clean history)

5. **Clean up** after the user confirms a PR is merged:
   - `git fetch origin && git checkout main && git pull`
   - `git branch -d <branch-name>`

### Worktree usage (only when explicitly requested)

When the user asks to use a worktree or work on multiple issues in parallel:
   - Create a worktree: `git worktree add .claude/worktrees/2-improve-parsing -b 2-improve-parsing`
   - All file reads/edits/writes must use the full worktree path, e.g. `.claude/worktrees/<branch-name>/src/...`
   - Run all git commands in the worktree using `-C`: `git -C .claude/worktrees/<branch-name> <command>`
   - Do NOT use `cd .claude/worktrees/<branch-name> && git ...` — compound `cd` + `git` commands require special approval
   - Cleanup: `git -C <repo-root> worktree remove .claude/worktrees/<branch-name>` then `git -C <repo-root> branch -d <branch-name>`

## Documentation Updates

When closing issues via PR, consider updating:
- **README.md** — Setup instructions, configuration, user-facing changes
- **docs/pdf-format.md** — PDF input format documentation
- **docs/csv-format.md** — CSV output format documentation
- **CLAUDE.md** — Technical implementation details, architecture, known issues, build commands

## Build Commands

```bash
dotnet build                                   # Build all projects
dotnet test                                    # Run all tests
dotnet run --project src/AbancaCardParser.Cli  # Run the parser (uses ./input, ./output defaults)

# Override input/output via env vars:
AbancaCardParser__InputDir=./local-test-data \
  AbancaCardParser__OutputDir=./local-test-data/output \
  AbancaCardParser__LogDir=./local-test-data/output \
  dotnet run --project src/AbancaCardParser.Cli
```

## Architecture

Two-project layout:

- **AbancaCardParser.Core** (`src/AbancaCardParser.Core/`): PDF text extraction, statement parsing, CSV writing. No dependencies on the CLI.
- **AbancaCardParser.Cli** (`src/AbancaCardParser.Cli/`): Console executable. Reads configuration, loops over PDF files, calls Core, writes output.

### Key types (in Core)

- `CardTransaction` (Models): date, description, amount, IsDebit, IsAmortizacionDeuda
- `CardStatement` (Models): list of transactions + optional stated total from TOTAL OPERACIONES TARJETA line
- `IPdfTextExtractor` (Parsing): interface — extracts text lines from a PDF stream
- `PdfPigTextExtractor` (Parsing): PdfPig implementation — groups words by Y-coordinate per page
- `StatementTextParser` (Parsing): converts extracted text lines → CardStatement using regex matching
- `BankCsvWriter` (Output): renders a CardStatement to the Abanca bank-account CSV format

### PDF parsing notes

- Only data from inside the PDF is used — filenames are never parsed.
- Year disambiguation: transaction dates in the PDF use `dd-mm-yy` (2-digit year). The parser scans for the first `dd-mm-yyyy` date in the PDF (e.g., FECHA COBRO on page 1) to derive the century prefix.
- The `TOTAL OPERACIONES TARJETA` line signals end-of-transactions and provides the stated total for verification. The stated total equals sum(D) − sum(H excluding AMORTIZACION DEUDA).

## Tech Stack

- **Language/Runtime**: C# / .NET 10
- **PDF parsing**: PdfPig (MIT)
- **Configuration**: Microsoft.Extensions.Configuration (JSON + Environment Variables)
- **Logging**: Microsoft.Extensions.Logging (console)
- **Testing**: MSTest

## Dependency Policy

Minimize external dependencies. Only add well-established, widely-used libraries when genuinely needed.

## Coding Conventions

- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Prefer records for DTOs
- Use `CancellationToken` for async operations where applicable
- Tests use MSTest with descriptive method names
- MSTest analyzers enforce strict assertion methods (MSTEST0037 is an error, not a warning):
  - Use `Assert.HasCount(expected, collection)` not `Assert.AreEqual(expected, collection.Count)`
  - Use `Assert.IsEmpty(collection)` not `Assert.AreEqual(0, collection.Count)`

## Test Classification

All tests in this repo are unit tests. No `[TestCategory("Integration")]` attribute should be needed — tests use only synthetic in-process data (no real PDFs, no external resources).

## Configuration

| Key | Env var | Default | Description |
|-----|---------|---------|-------------|
| `AbancaCardParser:InputDir` | `AbancaCardParser__InputDir` | `./input` | Directory containing input PDF files |
| `AbancaCardParser:OutputDir` | `AbancaCardParser__OutputDir` | `./output` | Directory for output CSV files |
| `AbancaCardParser:LogDir` | `AbancaCardParser__LogDir` | `./output` | Directory for per-file log files |

## Running via Docker Compose

```bash
docker compose run --rm parser
```

Example cron entry:
```cron
*/15 * * * * cd /path/to/abanca-credit-card-statement-parser && /usr/local/bin/docker compose run --rm parser >> /var/log/abanca-parser.log 2>&1
```

## Idempotency

The tool skips any PDF that already has a `.pdf.success.log` in the log directory. To reprocess a file, delete its `.success.log`. Files with only an `.error.log` are retried on the next run.
