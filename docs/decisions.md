# Architecture decisions — 2026-10-29

1. **ASP.NET Core Razor Pages, .NET 10.** Matches Matt's C# experience. Server-rendered forms keep deployment and state management small. No JavaScript or agent framework is needed. A pinned PdfPig library now provides independent PDF page counting and scan detection.
2. **AI interprets; code controls.** `IInvoiceExtractor` is replaceable. OpenAI Responses receives a document/image and returns a strict JSON schema. Decimal checks, locked coding pairs and workflow gates run locally. Model output remains untrusted; schema compliance is not factual correctness. No tools are exposed to the extraction model.
3. **Automate routine processing; review the draft once in Workday.** The target flow should not require routine line-by-line review in the assistant and a second review in Workday. Automated validation, completeness, duplicate, and supported-mapping checks run before draft preparation; blocking exceptions stop the affected work for resolution. Keep extracted records UNVERIFIED until the final human source review in Workday before submission. No Submit action is implemented. Real API extraction remains approval-gated and requires explicit user consent when authorized.
4. **Local durable JSON first.** One process, one user, atomic file replacement and an exclusive directory lock. Original bytes and processing events survive restarts. This avoids a database dependency for the initial milestone. Before production/multiple writers, migrate to a transactional store with unique constraints, revisions and shared processing history.
5. **Mock Workday only.** The gateway exposes Save for Later and readback, no submit/delete/cancel operations. Mock results use a separate persisted file, allowing interruption after write and before record completion to be reconciled. The mock checks supplier, total, line count and status; it does not establish live cart/line correctness.
6. **Workday access remains a discovery gate.** Official Workday documentation publishes SOAP WSDL/XSD and REST reference directories. Availability does not establish BYU authorization or draft semantics. Ask BYU's Workday/finance integration owner which supported procurement integration (SOAP/REST, approved EIB or other integration) can read suppliers, coding, existing requisitions and invoice references, create an unsubmitted draft, and read it back under the appropriate identity. Confirm API client/security domains, approved credentials, tenant/version, delegated-request permissions and test tenant. Do not assume a service identity is equivalent to Matt's account.
7. **Browser automation is deferred.** Only consider an approved locally installed automation runtime after supported integrations are assessed. Login/MFA may require Matt. Codex's browser tools are not a runtime dependency. Never automate Submit, never retry an ambiguous create, and independently read back actual line details before marking a live draft verified.

## Sources checked

- [Workday SOAP API reference](https://community-content.workday.com/en-us/public/products/platform-and-product-extensions/soap-api-reference.html): public API directory, not BYU access confirmation.
- [OpenAI file inputs](https://developers.openai.com/api/docs/guides/file-inputs): PDF input can include page images; inline file data is supported.
- [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses): JSON schema output; refusal/incomplete output must be handled.

No BYU tenant access, supplier IDs, accounting IDs, or live Save for Later operation has been verified. The supplied authenticated entry URL was preserved in requirements and was not used to create a requisition.

## Sample-development update

The expanded contract separates ordered/shipped quantities, pack sizes and priced pounds. Live draft mapping for weights remains a review gate. See extraction-contract.md and sample-validation.md. Scanned documents use vision through the existing OpenAI adapter, with manual local JSON import for development without credentials. PdfPig 0.1.16 is pinned: https://www.nuget.org/packages/PdfPig/0.1.16 . Development-only status is stored outside the imported invoice and blocks gateway writes.

