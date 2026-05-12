# abanca-credit-card-statement-parser

Converts Abanca VISA credit card PDF statements to CSV files in the Abanca bank-account format, ready for import into Firefly III.

## What it does

- Reads all `*.pdf` files from an input folder
- Outputs a `<name>.pdf.csv` file per PDF in the output folder
- Writes a `<name>.pdf.success.log` (or `.error.log`) alongside the CSV
- Skips files that already have a success log (safe to re-run as a cron job)

## Prerequisites

.NET 10 SDK **or** Docker with Compose.

## Quick start — local

```bash
mkdir -p input output
# Copy your Abanca credit card PDF(s) into ./input
dotnet run --project src/AbancaCardParser.Cli
# CSV and log files appear in ./output
```

## Quick start — Docker Compose

Edit `compose.yml` to adjust the volume mounts to your input/output directories, then:

```bash
docker compose run --rm parser
```

No `Dockerfile` required — uses the official `mcr.microsoft.com/dotnet/sdk:10.0` image directly.

## Running as a cron job

```cron
*/15 * * * * cd /path/to/abanca-credit-card-statement-parser && /usr/local/bin/docker compose run --rm parser >> /var/log/abanca-parser.log 2>&1
```

Exit code is `0` when all PDFs processed successfully, non-zero if any file failed.

## Configuration

| Environment variable | Default | Description |
|---|---|---|
| `AbancaCardParser__InputDir` | `./input` | Folder containing PDF files |
| `AbancaCardParser__OutputDir` | `./output` | Folder for output CSV files |
| `AbancaCardParser__LogDir` | `./output` | Folder for per-file log files (can equal OutputDir) |

Can also be set via `appsettings.json` using the `AbancaCardParser` section.

## Formats

- [PDF input format](docs/pdf-format.md)
- [CSV output format](docs/csv-format.md)

## Building and testing

```bash
dotnet build   # should produce zero warnings
dotnet test    # all tests are unit tests, no external resources needed
```
