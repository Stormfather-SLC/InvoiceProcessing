Build a focused AI-powered application called Workday Invoice Assistant.

## About me

My name is Matt. I am the Catering Supervisor at BYU Aspen Grove Family Camp. I use Windows, and I have experience with C#/.NET, Razor Pages, PowerShell, and Git.

I want to learn agent development while building a dependable tool. Explain major decisions in practical language.

## Goal

I want to upload a digital invoice or receipt and have the application reliably prepare the appropriate BYU Workday requisition under my account.

Start with my own requests. Later, support requests on behalf of another employee where Workday permissions permit.

The normal outcome is a verified requisition saved as Draft using Save for Later. Never submit without my separate, explicit instruction for that specific requisition.

## Architecture preferences

- Start with OpenAI for the AI component, but make it replaceable with another provider.
- Use AI for document interpretation and coding suggestions.
- Use programmed checks for arithmetic, required fields, coding rules, and allowed actions.
- Prefer a small, maintainable application. Consider ASP.NET Core/Razor Pages because I know that ecosystem; explain if another approach offers a significant benefit.
- Support development on two Windows computers through one private GitHub repository.
- Keep durable requirements, decisions, setup instructions, and progress in repository documentation.
- Do not assume that this Codex conversation’s browser tools will be available to the finished application.
- Do not assume that I have access to a Workday API. Investigate supported integration options before committing to browser automation.
- Assume that login or MFA may require my participation.

## Desired workflow

1. Upload a PDF or receipt image.
2. Extract supplier, invoice number, invoice date, total, and every line’s quantity, UOM, description, unit price, and extended amount.
3. Automatically validate extracted fields and reconcile the invoice total using decimal arithmetic. Do not require routine line-by-line user review in the assistant; keep source/model results UNVERIFIED until the user reviews the prepared Workday draft before submission.
4. Distinguish unpaid invoices from already-paid receipts. Do not assume both should create the same type of transaction.
5. Automatically stop and surface only blocking exceptions (such as missing pages, unresolved supplier/category, arithmetic mismatch, or unsupported pricing) that prevent a safe draft. Do not ask for routine duplicate line review in the assistant.
6. Check both the application’s processing history and Workday for possible duplicates.
7. Prepare one Workday line per invoice line.
8. Verify the cart and checkout details.
9. Use Save for Later.
10. Verify the saved requisition in Workday and record its number, total, line count, and Draft status.

## Batch and category workflow goals — 2026-11-02

- The target user experience is close to drag-and-drop: the user provides one or more invoices, and the system prepares the resulting Workday drafts and expense-log entries with minimal interaction.
- A Workday draft requisition is created for each distinct invoice + coding-pair category. The existing Coding suggestion pair (for example `SC6110|DHBEV`) is the category key. Keep one Workday line per invoice line; group lines with the same coding pair into that category's requisition. Different pairs on one invoice produce separate drafts.
- Create one SharePoint Dining Hall Budget and Spend Log row for each invoice + coding-pair category. Here, an **invoice/category pair** means one specific invoice grouped with one distinct Coding suggestion pair. Map the pair to its human-readable Purchase Category using the locked coding map (for example `SC6110|DHBEV` → `Beverages`; `SC6110|DNGHL` → `Food`). The required **Total Cost** is the subtotal of that invoice's lines in the group; multiple lines with the same pair are summed. Matt confirmed this rule on 2026-11-02.
- The user should not need to review every line in the assistant and then repeat that review in Workday. The intended human review is in Workday immediately before the user submits the draft. The assistant must not submit.
- Keep validation, duplicate prevention, completeness handling, persisted intent, uncertain-write reconciliation, and independent Workday readback. These are automated safeguards, not extra user review screens.
- This is the target workflow, not current capability. Workday remains mock-only and SharePoint writes are not implemented. External API and institutional data handling approval remains unresolved; real BYU invoices must not be sent to the OpenAI API until approved.

### SharePoint list schema observed from user-provided settings screenshot

The Dining Hall Budget and Spend Log has these visible columns:

| Column | Type | Required |
|---|---|---|
| Purchase Description | Single line of text | Yes |
| Transaction Date | Date and Time | Yes |
| Total Cost | Currency | Yes |
| Vendor | Choice | Yes |
| Receipt/Order # | Single line of text | No |
| Class ID | Choice | No |
| DH_Category | Choice | No |
| Finance Category | Choice | No |
| Purchase Category | Choice | Yes |

Modified, Created, Created By, and Modified By are SharePoint-managed columns. Choice values have not yet been provided for Class ID, DH_Category, and Finance Category. Exact mappings for those optional fields, Vendor, Purchase Description, and Transaction Date must be confirmed before writes. Purchase Category uses the locked coding map, and Total Cost uses the confirmed invoice/category subtotal rule above.

### Choice values observed from user-provided column settings screenshots

The required **Purchase Category** choice currently includes: Food, Beverages, Disposables, Food Service Supplies, Office Supplies, Janitorial Supplies, Linens, Rent/Lease, Equipment & Maintenance, Office Equipment, Large Equipment, Employee Development, Needs Review, and Membership/Dues. The column allows fill-in choices.

The required **Vendor** choice currently includes: BYU, Coca-Cola, Nicholas & Co, US Foods, Costco, Sam's Club, Amazon, Ecolab, SOS Pumping, Wal-Mart, WebsrestaurantStore, MSI, Harmons, Macey's, Pae-Barker, and Table Cloth Factory. The column allows fill-in choices.

These lists are expected to grow. Treat the SharePoint choice configuration as the source of truth; do not hardcode this snapshot as a closed list. Confirm whether fill-in values should be accepted for individual rows or formally added to the column choices. A user-added vendor/category must not silently create or change coding rules: mappings for new vendor and category cases need explicit configuration or must enter exception handling.

## Reliability requirements

- Preserve progress so interrupted work can be investigated and resumed.
- If a save might have succeeded, check Workday before attempting creation again.
- Never automatically retry an uncertain creation in a way that could produce a duplicate.
- Pause when a possible duplicate remains unresolved.
- Never guess unclear invoice data.
- Never report completion based only on an AI statement; verify the saved Workday result.
- Never cancel, delete, or modify an existing requisition without my explicit approval.
- Keep original documents and processing records outside the source-code repository.
- Keep credentials, API keys, and authenticated browser data outside Git.
- Provide useful status and error messages without exposing secrets.

## Established Workday process

Entry point:
https://www.myworkday.com/byu/d/task/2997$16885.htmld

Standard non-catalog settings:
- Request Type: Goods
- Unit of Measure: Each
- Supplier Item Identifier: blank
- Commodity Code: blank
- Order-From Connection: blank
- Supplier Contract: blank
- Line Memo: blank

Resolve the actual supplier for each invoice. Prior Coca-Cola requests have used Coca-Cola USA or Swire Coca-Cola, USA; do not assume these are interchangeable. Kessimakis Produce is another supplier.

Create a separate Workday line for every invoice line. Do not combine lines.

Request Description must preserve the complete invoice line, including quantity, original UOM, product description, unit price, and extended amount.

Example:
4 EA CF DT COKE (5.5+1) 2.5GA(9.5LT)CDBB 1LS $19.55 $78.20

Workday Quantity = invoice quantity.
Workday Price = invoice unit price.
Workday UOM = Each.

Before checkout, verify line count, descriptions, quantities, unit prices, extended amounts, and exact total.

Internal Memo:
Invoice [invoice number]

A prior Workday issue required a genuine edit-and-blur event before recognizing this memo. If encountered, click at the end, add a space, press Tab, and verify validation is cleared.

Final action: Save for Later. Never click Submit without separate, explicit authorization for that requisition.

## Locked coding map

- SC6110 Food and Beverages + DHBEV = Beverages
- SC6110 Food and Beverages + DNGHL = Food
- SC6111 Food Service Disposables + DNGHL = disposable cups, plates, napkins, cutlery, takeout containers, and similar items
- SC6112 Food Service Supplies + DNGHL = reusable or operational food-service supplies
- SC6115 Janitorial Supplies + DNGHL = chemicals and janitorial supplies
- SC6129 Supplies (not otherwise specified) + DHLIN = general supplies not otherwise specified
- SC6421 Employee Development Services + DNGHL
- SC6449 Rent/Lease <= 1 year + DNGHL
- SC6908 Equipment Maintenance (not otherwise specified) + DNGHL

For mixed invoices, classify each line individually. Ask about items that do not clearly fit. Treat these mappings as my established requirements; verify the corresponding selections in Workday.

## Completion report

Report:
- Invoice number
- Requisition number
- Supplier
- Total
- Number of lines
- Verified status
- Duplicate-check result
- Remaining steps, including attaching the invoice and submitting manually where applicable

Invoice attachment automation is a later enhancement unless we establish a reliable method.

## Start here

1. Inspect the project folder and any existing instructions.
2. Create AGENTS.md with the operating rules, especially duplicate prevention and the prohibition on unauthorized submission.
3. Create README.md, docs/requirements.md, docs/decisions.md, and docs/project-status.md.
4. Recommend a minimal architecture and explain the unresolved Workday access requirements.
5. Build the first local milestone: upload a document, extract structured invoice data, display every line for review, calculate totals, and show validation issues.
6. Use mock Workday results initially, clearly labeled as mock. Do not create live requisitions during the initial build.
7. Add meaningful checks for arithmetic, duplicate handling, and interrupted processing.
8. Document exactly what works, what is unverified, and the next step.

Keep scope focused on my invoice-to-draft workflow. Make routine implementation decisions yourself and ask me only when a missing answer materially changes the solution.
## Confirmed sample-development rules (2026-10-29)

Development samples must never create or modify live requisitions. Ordered quantity, shipped quantity, original UOM, pack size, billable weight and pricing basis must remain separate. Preserve out-of-stock zero-dollar items. Handwritten Y-numbers are historical references only. The Nicholas laundry/stain-removal chemical is confirmed SC6115 Janitorial Supplies + DNGHL; ignore its handwritten DHLIN note. Results remain unverified until independently checked. Keep actual sample data outside Git. Use only the explicitly supplied samples. A separate holdout pass is deferred until Matt intentionally supplies and authorizes those files.


## Latest scope clarification

Only the three explicitly supplied development samples and earlier Coca-Cola/Kessimakis references are authorized. Do not search for or inspect holdout files. A future holdout pass needs fresh explicit authorization. Their absence is not a blocker; this clarification supersedes any earlier direction to use adjacent holdout files.
