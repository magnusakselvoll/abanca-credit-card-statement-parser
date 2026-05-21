# firefly-iii-custom-uploader

Web app that parses credit-card statement PDFs, lets you review the parsed transactions, and uploads them directly to [Firefly III](https://www.firefly-iii.org/).

[![CI](https://github.com/magnusakselvoll/firefly-iii-custom-uploader/actions/workflows/ci.yml/badge.svg)](https://github.com/magnusakselvoll/firefly-iii-custom-uploader/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Supported statement formats

| Format | Description |
|--------|-------------|
| Abanca VISA Clásica | Spanish-locale PDF statements in the OpenText Exstream format (see [docs/pdf-format.md](docs/pdf-format.md)) |

Adding a new format is straightforward — see [Adding a new statement format](#adding-a-new-statement-format) below.

## How it works

1. Open the web UI, pick your Firefly III asset account, upload a PDF.
2. The app parses the PDF and shows you a table of transactions. Rows already present in Firefly III are shown as "Already in Firefly" and cannot be re-submitted. Rows you want to skip can be unchecked.
3. Click **Submit** — transactions are created in Firefly III with a run label (`ffcu-upload-<timestamp>`) for easy bulk rollback.

> **Idempotency:** Every transaction is assigned a deterministic `external_id`. Re-uploading the same PDF, or uploading overlapping statements, only creates the transactions that don't already exist in Firefly III.

> **AMORTIZACION DEUDA rows** (Abanca card-debt repayment lines) are shown but excluded by default. Firefly III models those as transfers between two accounts; the source account is not available in the PDF, so they require manual entry.

## Quick start — Docker Compose

```bash
curl -O https://raw.githubusercontent.com/magnusakselvoll/firefly-iii-custom-uploader/main/compose.yml
# Edit compose.yml: set FireflyIiiUrl and FireflyIiiToken
docker compose up -d
```

Then open `http://localhost:8080` in your browser.

To pin a specific release instead of `latest`:

```yaml
# compose.yml
image: ghcr.io/magnusakselvoll/firefly-iii-custom-uploader:v0.1.0
```

## Quick start — local (.NET SDK)

```bash
FireflyIiiCustomUploader__FireflyIiiUrl=http://your-firefly-host \
  FireflyIiiCustomUploader__FireflyIiiToken=your-token \
  dotnet run --project src/FireflyIiiCustomUploader.Web
```

Then open `http://localhost:8080`.

## Configuration

All configuration is via environment variables (double underscore = section separator).

| Environment variable | Default | Required | Description |
|---|---|---|---|
| `FireflyIiiCustomUploader__FireflyIiiUrl` | — | Yes | Base URL of your Firefly III instance (e.g. `http://firefly:8080`) |
| `FireflyIiiCustomUploader__FireflyIiiToken` | — | Yes | Firefly III personal access token |
| `FireflyIiiCustomUploader__WebListenUrl` | `http://0.0.0.0:8080` | No | Internal Kestrel bind URL |
| `FireflyIiiCustomUploader__RunTagPrefix` | `ffcu-upload` | No | Prefix for the per-run Firefly III tag |

Access control is network-level only (e.g. Tailscale ACLs or a reverse proxy) — there is no application-level login.

Can also be configured via `appsettings.json` under the `FireflyIiiCustomUploader` section.

## Building and testing

```bash
dotnet build   # should produce zero warnings
dotnet test    # all tests are unit tests, no external resources needed
```

## Adding a new statement format

1. Create a class in `src/FireflyIiiCustomUploader.Core/Parsing/<BankName>/` that implements `IStatementParser`:

   ```csharp
   public class MyBankParser : IStatementParser
   {
       public string FormatId => "mybank-visa";

       public bool CanParse(IReadOnlyList<string> lines) =>
           lines.Any(l => l.Contains("MY BANK STATEMENT MARKER"));

       public CardStatement Parse(IReadOnlyList<string> lines) { /* ... */ }
   }
   ```

2. Register it in `ServiceCollectionExtensions.AddFireflyIiiCustomUploader`:

   ```csharp
   services.AddSingleton<IStatementParser, MyBankParser>();
   ```

The registry tries parsers in registration order and uses the first match.

## Formats

- [PDF input format (Abanca)](docs/pdf-format.md)
- [CSV export format](docs/csv-format.md)

## License

Released under the [MIT License](LICENSE).

## Contributing

Issues and PRs are welcome. This is a small personal tool — responses may be slow.
