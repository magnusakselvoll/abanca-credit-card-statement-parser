# CSV Output Format

The output CSV matches the column layout of Abanca's bank-account transaction export, so both can be imported into Firefly III using the same import profile.

## Encoding and line endings

- **Encoding**: UTF-8 without BOM
- **Line endings**: CRLF (`\r\n`)
- **Column separator**: `;` (semicolon)

## Header

```
Fecha ctble;Fecha valor;Concepto;Importe;Moneda;Saldo;Moneda;Concepto ampliado
```

## Columns

| # | Name | Format | Notes |
|---|---|---|---|
| 1 | Fecha ctble | `dd-MM-yyyy` | Same as Fecha valor (credit cards have one date per transaction) |
| 2 | Fecha valor | `dd-MM-yyyy` | Per-line transaction FECHA from the PDF |
| 3 | Concepto | Text | Description from the PDF, trimmed |
| 4 | Importe | Spanish decimal | Negative for D (debit/charge), positive for H (credit/refund) |
| 5 | Moneda | `EUR` | Always EUR |
| 6 | Saldo | *(blank)* | Left empty; credit cards don't carry a per-transaction running balance |
| 7 | Moneda | `EUR` | Always EUR |
| 8 | Concepto ampliado | *(blank)* | Left empty; PDF has no extended description |

## Importe format

Uses Spanish locale notation (comma as decimal separator, no thousands separator):

| Transaction | IsDebit | Importe |
|---|---|---|
| `APPLE.COM/BILL ITUNES.COM 9,99 D` | true | `-9,99` |
| `HOTEL REFUND 147,14 H` | false | `147,14` |
| `AMORTIZACION DEUDA 135,94 H` | false | `135,94` |

The previous-month payment (**AMORTIZACION DEUDA**) is included as a positive `Importe`. When importing both the bank-account CSV and the credit-card CSV into Firefly III, reconcile this row against the corresponding outgoing transfer in the bank-account export.

## Example row

```
18-02-2026;18-02-2026;APPLE.COM/BILL ITUNES.COM;-9,99;EUR;;EUR;
```
