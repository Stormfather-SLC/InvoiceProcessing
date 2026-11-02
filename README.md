# Workday Invoice Assistant

Matt's local invoice review application for BYU Aspen Grove. **Workday is mock-only. There is no live creation or submission code.**

## Run on Windows

Install the .NET 10 SDK (a runtime alone cannot build). In PowerShell at this repository:

```powershell
dotnet run --project src/WorkdayInvoiceAssistant
```

Open http://127.0.0.1:5187. Select **Open synthetic example** to exercise validation and mock Save for Later without an API key. This example is explicitly synthetic, not extraction of your upload.

Optional AI extraction requires BYU approval for the specific invoice data flow before real BYU invoices are sent. The app keeps this option collapsed so local processing is the primary workflow. If approved, configure a vision-capable Responses API model that supports structured output and is available to your account:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_KEY" --project src/WorkdayInvoiceAssistant
dotnet user-secrets set "OpenAI:Model" "YOUR_MODEL_ID" --project src/WorkdayInvoiceAssistant
```

Select up to 20 PDF, PNG or JPEG invoices in one batch (10 MB each, 50 MB total). Without API approval, use local transcription/import; no invoice is sent externally. The expandable correction editor accepts structured JSON; null means unknown. Changes are recorded and revalidated. Each invoice is grouped by Coding suggestion pair: each group becomes a separate mock draft and retains one line per original invoice line. The SharePoint preview shows one row per invoice/category pair and the confirmed category subtotal, while flagging fields or live choice lookups that remain unresolved. SharePoint writes are not implemented. Mock draft creation requires automated validation and a clean local duplicate check, but does not record a human source comparison; the record remains UNVERIFIED. In the intended future workflow, users perform their final source review in Workday before submitting. This milestone has no live Workday connection or submission capability.

```powershell
dotnet run --project tests/Checks
```

The checks use synthetic invoices and fake API responses, with no external service calls or NuGet test framework. The app uses the pinned PdfPig package for local PDF page counting and scan detection; package restore needs network access on first setup.

## Development samples and review

Check **Development sample** when uploading a test invoice. This restriction persists through re-upload/import/corrections and prevents even mock draft creation. PDF inspection counts actual pages and identifies image-only scans independently of AI. The OpenAI provider sends PDFs as file inputs for a vision-capable model; it never interprets empty extracted text as an empty invoice.

The review table separates ordered quantity, shipped quantity, pack size, original UOM, billable weight, pricing basis, pricing quantity, unit price and extended amount. Use **Import locally transcribed invoice JSON** to review external sample data without an API key. Imports are clearly labeled manual and remain UNVERIFIED. The JSON correction editor preserves previous revisions and invalidates an earlier independent check. Record reviewer/evidence only after comparing every source field. Download the validation report from each record.

Weight-priced amounts are checked using pounds. Their Workday Each/quantity/price mapping remains blocked for review. Missing pages, estimates, unresolved coding, out-of-stock items and uncertain readings also block saving. The application never silently drops zero-dollar lines.

See [extraction contract](docs/extraction-contract.md) and [development sample results](docs/sample-validation.md). Actual sample documents, transcriptions and full reports are outside this repository.

## Data and recovery

Data defaults to `%LOCALAPPDATA%\WorkdayInvoiceAssistant`, outside Git. `DataRoot` can point to another local folder outside the repository. Keep this folder private to your Windows account; it contains original invoices and review records in plaintext. Back it up securely. Run one application instance per data directory. Atomic record replacement, flushed writes, and a directory ownership lock protect local progress. Corrupt history stops processing rather than silently bypassing duplicate checks.

An interrupted extraction becomes `ExtractionInterrupted`; explicitly retry from its record. A save intent becomes `SaveUncertain`; use reconciliation, which reads the mock result without creating anything. An unresolved uncertain save stays blocked. Do not delete records to bypass duplicate checks. No automated live retry exists.

The app listens on loopback only, validates Host, uses antiforgery forms and has no authentication system for remote hosting. Do not expose it to a network. Originals download as attachments. Raw service responses and secrets are not put into processing events.

## Two computers / private GitHub repository

This folder is a Git repository but has no remote. Create one private GitHub repository under your chosen owner, then add its URL as `origin`, commit source, and push. Clone it on the second Windows computer. Install .NET 10 and configure user-secrets independently on each computer. Never commit documents, records, keys or browser profiles. Pull before work; commit and push after work.

Local processing history is **not synchronized**. Do not process the same production workload independently on two computers. A shared authoritative history and live Workday duplicate lookup are prerequisites for multi-computer production use. Initial development and synthetic testing can happen on either computer.

See [requirements](docs/requirements.md), [decisions](docs/decisions.md), and [project status](docs/project-status.md).
