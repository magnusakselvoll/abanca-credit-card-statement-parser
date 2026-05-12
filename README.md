# abanca-credit-card-statement-parser

Converts **Abanca VISA Clásica** credit card PDF statements (Spanish locale, OpenText Exstream format) to CSV files in the Abanca bank-account format, ready for import into Firefly III.

> **Note:** This tool only supports Abanca VISA Clásica statements in the PDF format described in [docs/pdf-format.md](docs/pdf-format.md). Other banks and other Abanca products are not supported.

[![CI](https://github.com/magnusakselvoll/abanca-credit-card-statement-parser/actions/workflows/ci.yml/badge.svg)](https://github.com/magnusakselvoll/abanca-credit-card-statement-parser/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## What it does

- Reads all `*.pdf` files from an input folder
- Outputs a `<name>.pdf.csv` file per PDF in the output folder
- Writes a `<name>.pdf.success.log` (or `.error.log`) alongside the CSV
- Skips files that already have a success log (safe to re-run as a cron job)

## Quick start — Docker Compose (no clone needed)

```bash
mkdir -p input output
# Copy your Abanca credit card PDF(s) into ./input
curl -O https://raw.githubusercontent.com/magnusakselvoll/abanca-credit-card-statement-parser/main/compose.yml
docker compose run --rm parser
# CSV and log files appear in ./output
```

To pin a specific release instead of `latest`:

```yaml
# compose.yml
image: ghcr.io/magnusakselvoll/abanca-credit-card-statement-parser:v0.1.0
```

## Quick start — local (.NET SDK)

```bash
mkdir -p input output
# Copy your Abanca credit card PDF(s) into ./input
dotnet run --project src/AbancaCardParser.Cli
# CSV and log files appear in ./output
```

## Running as a cron job

```cron
*/15 * * * * cd /path/to/compose-dir && /usr/local/bin/docker compose run --rm parser >> /var/log/abanca-parser.log 2>&1
```

Exit code is `0` when all PDFs processed successfully, non-zero if any file failed.

## Configuration

| Environment variable | Default | Description |
|---|---|---|
| `AbancaCardParser__InputDir` | `/data/input` | Folder containing PDF files |
| `AbancaCardParser__OutputDir` | `/data/output` | Folder for output CSV files |
| `AbancaCardParser__LogDir` | `/data/output` | Folder for per-file log files (can equal OutputDir) |

Can also be set via `appsettings.json` using the `AbancaCardParser` section.

## Formats

- [PDF input format](docs/pdf-format.md)
- [CSV output format](docs/csv-format.md)

## Building and testing

```bash
dotnet build   # should produce zero warnings
dotnet test    # all tests are unit tests, no external resources needed
```

## License

Released under the [MIT License](LICENSE).

## Contributing

Issues and PRs are welcome. This is a small personal tool — responses may be slow.
