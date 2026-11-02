# Reference-only evaluation — 2026-10-29

The user provided two historical invoices strictly for extraction and validation, with an explicit prohibition on creating another requisition. They were read locally, not uploaded into the application, sent to the OpenAI API, or used to create a mock/live requisition. No Workday lookup was performed. Source PDFs, rendered pages, exact invoice values and structured reference records remain outside this repository.

## Findings and implementation implications

- Text-based and image-only PDFs both occur. Empty text extraction must not be mistaken for an empty invoice. The scanned example required visual transcription.
- Preserve zero-dollar lines. They contribute to line count even when they do not change totals; flag their coding and transaction treatment for review.
- Keep case pack notation separate from billable quantity. Ordered and shipped columns must be distinguished; a mismatch requires review.
- Preserve leading zeros in invoice identifiers and product codes in source descriptions.
- Billing/remit names and ship-from entities may differ. Never automatically substitute one as the Workday supplier.
- Historical amount-due labels and net payment terms do not establish current payment status. Completed-folder names and requisition-like filename prefixes suggest prior processing but do not verify Workday status.
- Conditional late-payment boilerplate is not an actual invoice charge.

All 19 reference lines reconciled individually using decimal arithmetic, and both invoice totals matched. This establishes a manually reviewed comparison reference, not the accuracy of the application's OpenAI extraction. Full reference records are in this chat's external artifact directory under `reference-review`, explicitly marked reference-only and creation-prohibited.

The extraction prompt now includes these distinctions. Deterministic validation flags zero-dollar lines without dropping them. Synthetic checks cover zero-dollar review, pack-size preservation and leading-zero identifiers. Real provider evaluation still requires an approved configured OpenAI model and key; no claims of provider accuracy are made.
