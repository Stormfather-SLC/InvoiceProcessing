# Project status — 2026-10-29

## Working

- Local Razor Pages upload and durable original/history retention outside Git.
- Independent PDF page counting and scan detection; replaceable OpenAI vision extraction adapter with strict structured schema; local transcription JSON import without an API key.
- Review of every line with ordered/shipped quantities, UOM, pack size, billable weight, pricing basis, price, amount, source text and proposed coding.
- Decimal reconciliation; confirmed laundry-chemical coding; missing-page, estimate, unknown-field, zero-charge and weight-mapping review gates.
- Reference-only uploads that block even mock draft creation; UNVERIFIED status until human source comparison; revisions invalidate prior verification.
- Local duplicates, interrupted extraction recovery, uncertain-save reconciliation and independent mock result readback. No Submit/delete/cancel/live integration code.

## Authorized sample status

Only the three explicitly supplied development samples plus the earlier Coca-Cola and Kessimakis invoices are active: five documents, six pages, 53 lines. Full findings and values are outside Git. All results remain UNVERIFIED. See [sample validation](sample-validation.md) and [extraction contract](extraction-contract.md).

Correction: two adjacent holdout files were previously accessed/imported without Matt's intended authorization. Their records and derivatives are now archived outside active history/evaluation. The prior mixed-scope report is superseded. No further holdout inspection is permitted without fresh explicit authorization; their absence is not a blocker. Original files and completed authorized work were preserved.

## Verification performed

- Build and 43 synthetic checks passed on Windows with .NET SDK 10.0.403.
- Five authorized HTTP workflows passed, including actual page counts, imports, UNVERIFIED exports and blocked mock-save attempts. Excluded records are absent from active history.
- Local app records were copied with hash verification from the temporary smoke-test directory to the normal local application data folder; originals retained. The app now uses its normal durable data folder.
- No live API extraction run has been validated. Sample transcriptions came from local text/visual review; arithmetic verification does not establish factual extraction accuracy.

## Remaining

Confirm completeness, final payable amounts and all proposed coding with independent human review. Real OpenAI extraction requires an approved configured key/model. BYU integration permissions, supplier/coding IDs, read-only duplicate search, MFA and actual draft-save/readback semantics are unverified. Weight-priced Workday mapping and unshipped zero-charge treatment remain blocked. Invoice attachments, on-behalf-of requests and shared multi-computer processing history are deferred. A private GitHub remote is not yet configured. The correction UI remains JSON-based, and local data is plaintext.

Next step: independently check the five authorized references, evaluate the real extraction adapter against the corrected data, and obtain BYU's supported integration requirements. Keep Workday mocked throughout this phase.

## First live extraction check — 2026-11-02

API key configuration was confirmed without displaying the key. One paid extraction call through the existing OpenAI adapter succeeded using the configured gpt-4.1-mini model on the authorized chemical reference. Invoice number/date, line count, ordered/shipped quantities, unit price, extended amount, total and confirmed coding matched the prior unverified transcription. Supplier differed only in capitalization. Currency was not explicitly stated and completeness/current payment status remained unknown. The result remains UNVERIFIED; this is a single adapter smoke check, not established accuracy. The earlier manual record was preserved. Request intent, extracted data and comparison are outside Git in the chat artifact directory. No Workday action occurred.

## Review-flow simplification — 2026-11-02

The review page keeps optional OpenAI extraction collapsed and states that BYU approval is required before sending real invoices. Mock draft creation no longer requires a pre-draft user source comparison; it remains gated by automated validation, page/completeness rules, local duplicates and reference-only status. Records remain UNVERIFIED until an independent source comparison is recorded. The intended future final review is in Workday before submission; this milestone still has no live Workday connection or submission capability. Build succeeded after the UI and handler change using a temporary output folder because the user had the app binary running; NuGet vulnerability metadata could not be reached.

## Batch and category workflow implementation — 2026-11-02

- Upload form accepts up to 20 invoice files in a batch (10 MB each; 50 MB aggregate), processes each independently, and reports accepted, duplicate, and rejected files.
- Mock preparation groups invoice lines by Coding suggestion pair, preserves each source line, and persists/read-backs one mock draft per pair. Per-group intent and uncertain outcomes are reconciled without automatic retry.
- Each invoice now displays one SharePoint row preview per coding pair, with category subtotal in Total Cost. The preview shows required and optional list fields and explicitly marks unknown description format, proposed date/vendor/receipt mappings, live choice verification, and unmapped optional fields.
- SharePoint remains preview-only: no SharePoint connector or live write. Its choice lists are not embedded as closed constants; future writes must query the live list choices and handle additions.
- No user line-by-line review is required in the assistant. Drafts remain UNVERIFIED; final human review is intended in Workday before submission. Workday is still mock-only, with no submission capability.

## Simplified intake screen — 2026-11-02

- Reworked the home page around the requested path: select files, click Start, see a compact status, and use a Workday task link after mock preparation.
- Moved line data, SharePoint preview, duplicate/validation details, recovery/import tools, history, and synthetic demo controls into collapsed optional details.
- Added upload-in-progress feedback and clear status steps. The current app only stores/checks uploaded files; external extraction remains paused pending institutional approval, and Workday remains mock-only. The Workday link opens the general requisition task and does not represent an app-created requisition.
