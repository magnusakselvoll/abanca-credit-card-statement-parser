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
dotnet build                                            # Build all projects
dotnet test                                             # Run all tests
dotnet run --project src/FireflyIiiCustomUploader.Web   # Run the web app (listens on http://localhost:8080)

# Override config via env vars:
FireflyIiiCustomUploader__FireflyIiiUrl=http://firefly:8080 \
  FireflyIiiCustomUploader__FireflyIiiToken=my-token \
  dotnet run --project src/FireflyIiiCustomUploader.Web
```

## Architecture

Two-project layout:

- **FireflyIiiCustomUploader.Core** (`src/FireflyIiiCustomUploader.Core/`): PDF text extraction, statement parsing, Firefly III HTTP client, upload planning/execution. No web dependencies.
- **FireflyIiiCustomUploader.Web** (`src/FireflyIiiCustomUploader.Web/`): ASP.NET Core minimal-API web host. Serves the upload form, review page, and submit action.

### Key types (in Core)

**Models**
- `CardTransaction` — date, description, amount, IsDebit, Category (optional; populated by parsers that have category data)
- `CardStatement` — list of transactions + optional stated total from TOTAL OPERACIONES TARJETA line

**Parsing**
- `IPdfTextExtractor` — extracts text lines from a PDF stream
- `PdfPigTextExtractor` — PdfPig implementation; groups words by Y-coordinate per page
- `IStatementParser` — `{ FormatId, DisplayName, AccountNameHint, CanParse(lines), Parse(lines) }` — one implementation per bank/format; `DisplayName` is shown in the format dropdown; `AccountNameHint` is a regex matched case-insensitively against Firefly account names to pre-select the best-guess account
- `StatementParserRegistry` — `FindParser(lines)` tries each `CanParse` in registration order; `GetParser(formatId)` looks up by id; `Parsers` exposes the full list
- `Parsing/Abanca/AbancaStatementParser` — parses Abanca VISA Clásica PDF statements; sniffs on "TOTAL OPERACIONES TARJETA"; `FormatId = "abanca-visa"`
- `Parsing/Abanca/AbancaWebCopyStatementParser` — parses tab-separated rows copied from Abanca's web banking UI; sniffs on a valid data row (TIT. | dd/MM/yyyy | … | N,NN EUR); negative amounts = debit; TIPO OPERACIÓN written to `Category` (stored in Firefly `notes`); `FormatId = "abanca-web-copy"`; reuses `AbancaStatementParser.ParseSpanishDecimal`
- `Parsing/Advanzia/AdvanziaStatementParser` — parses Advanzia card exports; sniffs on column header containing "Counterparty" and "Category"; `FormatId = "advanzia"`; negative amounts = debit, positive = credit; category token written to Firefly `notes` as `"Category: <value>"`

**Firefly III**
- `IFireflyIiiClient` / `FireflyIiiClient` — paginated GET accounts, GET transactions, POST transaction
- `LenientDateOnlyConverter` — handles Firefly III's ISO datetime strings as well as plain `yyyy-MM-dd`

**Sync**
- `ExternalIdFactory` — deterministic `external_id = "{formatId}:{sha1(date|amountCents|D/H|normalizedDescription)}"` for idempotency
- `UploadPlan` / `UploadPlanItem` / `UploadDecision` — plan record with per-item decisions (Create / SkipDuplicate)
- `UploadPlanner.BuildPlanAsync` — queries Firefly III for existing external_ids in the statement's date range; assigns decisions
- `UploadExecutor.ExecuteAsync` — creates `Create` items that the user included; stamps each with a run tag; returns `UploadResult`
- `TransactionMapper.ToTransactionSplit` — maps `CardTransaction` → Firefly `TransactionSplit` (debit=withdrawal, credit=deposit); populates `notes` from `CardTransaction.Category` as `"Category: <value>"` when set
- `BestGuessAccount.Match(accounts, hintRegex)` — returns the first account whose name matches the regex (IgnoreCase); returns null when hint is null or no match

**Options**
- `FireflyIiiCustomUploaderOptions` — FireflyIiiUrl, FireflyIiiToken, WebListenUrl, RunTagPrefix; bound from config section `FireflyIiiCustomUploader`

### PDF parsing notes

- Only data from inside the PDF is used — filenames are never parsed.
- Year expansion: transaction dates use `dd-mm-yy` (2-digit year), expanded to the current century (e.g. `26 → 2026`). This handles the common December → January boundary within a single century.
- The `TOTAL OPERACIONES TARJETA` line signals end-of-transactions and provides the stated total for verification. It also serves as the `CanParse` sniff marker for `AbancaStatementParser`.

### Idempotency

`UploadPlanner` always queries Firefly III for existing transactions in the statement's date range on the selected asset account, collecting every `external_id` found. Items whose synthetic `external_id` already exists get `SkipDuplicate`. The submit handler additionally validates that only items with decision `Create` are accepted, so even hand-crafted POSTs cannot force a duplicate.

### Web flow

1. `GET /` — renders the upload form: a file-picker section (PDF) and a textarea section ("paste statement text"), each with its own submit button (no Firefly III call needed here).
2. `POST /upload` — accepts either an uploaded `file` (PDF → `IPdfTextExtractor.ExtractLines`) or a `pastedText` form field (split on newlines). Produces `IReadOnlyList<string> lines` via either path, auto-detects best-guess format via `FindParser` (may be null), stores `PendingUpload(lines, detectedFormatId)` in `PendingUploadStore` with a 15-min TTL, redirects to `/select/{token}`.
3. `GET /select/{token}` — loads the pending upload; fetches asset accounts from Firefly III; pre-selects format (detected) and account (via `BestGuessAccount.Match` using the detected parser's `AccountNameHint`); renders format + account dropdowns.
4. `POST /select` — reads `token`, `formatId`, `accountId`; loads pending lines (non-destructive, allows retry); looks up parser by `formatId`; parses lines; builds `UploadPlan` (queries Firefly III for dedup); stores plan in `ReviewState` with a 15-min TTL, redirects to `/preview/{planToken}`. Re-renders the selection screen with an error on bad format, parse failure, or zero transactions.
5. `GET /preview/{token}` — renders the transaction table with checkboxes (disabled for SkipDuplicate).
6. `POST /submit` — reads form, re-validates included indices, calls `UploadExecutor`, renders result page.
7. `GET /download-csv/{token}` — uses `BankCsvWriter` to produce a downloadable CSV of all parsed transactions.

HTML is rendered via raw-string interpolation in `Web/Html.cs` — no JS framework, no template engine. Forms use plain POST. Checkboxes have `name="include"` + `value="{index}"` so unchecked rows simply absent from the POST body.

### Adding a new statement format

1. Create a parser class in `src/FireflyIiiCustomUploader.Core/Parsing/<BankName>/` implementing `IStatementParser`. Set:
   - `FormatId` — stable internal slug (e.g. `"mybank-visa"`).
   - `DisplayName` — user-facing label shown in the format dropdown (e.g. `"My Bank credit card"`).
   - `AccountNameHint` — case-insensitive regex matched against Firefly account names to pre-select the best-guess account (e.g. `"mybank.*credit"`; use `null` for no hint).
   - `CanParse` — content-sniff to identify this format from PDF lines.
   - `Parse` — produce a `CardStatement` from PDF lines.
2. Register it as `services.AddSingleton<IStatementParser, YourParser>()` in `ServiceCollectionExtensions.AddFireflyIiiCustomUploader`.

The registry tries parsers in `CanParse` order for auto-detection; `GetParser(formatId)` looks up a specific format when the user makes an explicit selection.

## Tech Stack

- **Language/Runtime**: C# / .NET 10
- **Web host**: ASP.NET Core (`Microsoft.NET.Sdk.Web`, Kestrel, minimal APIs)
- **PDF parsing**: PdfPig (MIT)
- **HTTP resilience**: Polly via `Microsoft.Extensions.Http.Resilience`
- **Configuration**: Microsoft.Extensions.Configuration (JSON + Environment Variables)
- **Logging**: Microsoft.Extensions.Logging (console, from ASP.NET Core host)
- **Testing**: MSTest
- **Deployment**: Docker via .NET SDK container publish (`dotnet publish -t:PublishContainer`) — no Dockerfile needed; uses `mcr.microsoft.com/dotnet/aspnet:10.0` base image

## Simplicity Policy

Prefer the simplest solution that works today. Do not guard against scenarios that are clearly out of scope (e.g. year-2100 date edge cases, hypothetical scale requirements). If the deployment context makes an assumption safe, use it. When you encounter an over-engineered pattern in existing code, flag it rather than copy it.

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

All tests in this repo are unit tests. No `[TestCategory("Integration")]` attribute should be needed — tests use only synthetic in-process data (no real PDFs, no real Firefly III).

## Configuration

| Key | Env var | Default | Description |
|-----|---------|---------|-------------|
| `FireflyIiiCustomUploader:FireflyIiiUrl` | `FireflyIiiCustomUploader__FireflyIiiUrl` | *(required)* | Base URL of the Firefly III instance |
| `FireflyIiiCustomUploader:FireflyIiiToken` | `FireflyIiiCustomUploader__FireflyIiiToken` | *(required)* | Firefly III personal access token |
| `FireflyIiiCustomUploader:WebListenUrl` | `FireflyIiiCustomUploader__WebListenUrl` | `http://0.0.0.0:8080` | Internal Kestrel bind URL |
| `FireflyIiiCustomUploader:RunTagPrefix` | `FireflyIiiCustomUploader__RunTagPrefix` | `ffcu-upload` | Prefix for the per-upload Firefly III run tag |

## Running via Docker Compose

```bash
docker compose up -d
```

Then open `http://localhost:8080`. The container exposes port 8080.
