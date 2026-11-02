# OpenAI API data-flow review

**Status:** Draft for BYU review. No approval is recorded by this document. Do not send real BYU invoices to the API until BYU confirms that this specific API use and data flow are permitted.

## Purpose and boundary

The local Workday Invoice Assistant helps an authorized BYU Aspen Grove user extract supplier-invoice fields for human review. Workday remains mock-only; this process does not create or submit requisitions. The API is optional. Local PDF inspection and manual transcription/import work without an API call.

## Proposed flow

1. A user selects a PDF, PNG, or JPEG (up to 10 MB) in the loopback-only Razor Pages app and explicitly consents before extraction.
2. The app saves the original document and a processing record locally before extraction. By default, the data root is `%LOCALAPPDATA%\\WorkdayInvoiceAssistant`; `DataRoot` can override it. The project requires originals and processing records to remain outside Git and the source tree. Local records are plaintext and the app has no user authentication; it is intended for one local user and loopback access.
3. On explicit extraction, the app sends the full uploaded PDF as a base64 `input_file`, or the full image as a base64 `input_image`, to OpenAI's Responses API. The configured model is read from local .NET user-secrets/configuration. The current code does not redact invoice content before transmission.
4. The model returns structured invoice data for local review. The result is stored in the local processing record, remains UNVERIFIED, and requires an independent comparison against the original. No Workday operation occurs in this flow.
5. The original, extracted result, revisions, and review history are retained locally. The application does not intentionally log the API key or raw API response to processing events. Provider-side retention, human access, abuse monitoring, and deletion behavior have not been established here and must be confirmed for the specific OpenAI account/API configuration.

## Data potentially transmitted

The source image/document is sent whole, so it may include any visible invoice content: supplier and remittance identity, addresses and contact details, invoice and purchase-order references, dates, line descriptions/item identifiers, quantities, UOM, prices, totals, taxes, shipping/fees, departmental or cost-center references, payment terms/status, handwritten annotations, and any bank, tax, card, or other sensitive details present on a page. The structured output may include supplier, invoice identifiers/date, currency, payment status, total, invoice lines, coding suggestions, page/completeness evidence, source text, and uncertainty concerns. Unknown values are meant to remain null; coding is advisory.

The app also sends technical request data required by the API, including the selected model and authentication credential. Confirm exact endpoint, account owner/type, region, subprocessors, and metadata handling with BYU and OpenAI before approval.

## Users, outputs, and controls

- **Users:** local project user; exact authorized user group is not yet defined.
- **Human review:** every result must be compared to the source; model output is not proof of completeness, coding accuracy, or Workday status.
- **Local storage:** original and processing history are outside Git, normally under the current Windows user's local application data. Access controls, backup, retention duration, and deletion schedule need BYU review.
- **API settings:** API account type, contract, retention/data-use settings, region, and any organization-level controls require confirmation. A ChatGPT product approval is not assumed to cover API use.
- **Workday:** no live connection, duplicate check, save, or submit is part of this flow. The gateway is mock-only.

## Decisions BYU needs to make

1. Classify supplier invoices and the proposed extracted fields; identify the responsible Data Steward and Information Trustee.
2. Confirm the business purpose, permissible fields, whether whole-document transmission is necessary, and whether redaction is required.
3. Confirm whether this OpenAI API account, endpoint, model, integration, and use case are approved, and what contract, Data Sharing Agreement, or privacy addendum is required.
4. Determine CES SOC assessment requirements for the vendor and local application; obtain OGC review and Purchasing guidance as applicable before any agreement, purchase, or API-credit commitment.
5. Set approved retention, logging, access, storage, backup, deletion, and incident-handling requirements for both provider and local records.
6. Define the authorized user population and required training or authentication controls.

## Review contacts and source references

Start with the assigned financial/supplier-data Data Steward and department CSR/OIT representative to classify the data and confirm the use case. BYU's published Data Use, Privacy, and Security Policy directs employees to contact OGC and CES SOC before entering an arrangement with a third party that processes or accesses nonpublic institutional data. The CES SOC Vendor Security Risk Assessment Procedure describes when an assessment is required and how to request one. Purchasing should confirm purchasing method and authority.

- [BYU Data Use, Privacy, and Security Policy](https://policy.byu.edu/view/data-use-privacy-and-security-policy)
- [BYU Guiding Principles for the Use of AI](https://genai.byu.edu/00000191-484f-db4d-a995-e9cf5a1d0001/guiding-principles-of-ai)
- [CES SOC Vendor Security Risk Assessment Procedure](https://infosec.byu.edu/vendorsecurityriskassessment)

This is an engineering data-flow summary for review, not an official BYU classification, approval, or legal determination. Until the required BYU determinations are documented, use only synthetic data for API testing.
