# Extraction and validation contract

## Line evidence

- `OrderedQuantity` and `ShippedQuantity` are separate source values. `Quantity` remains only for backward-compatible reading of older records and is not the arithmetic basis.
- `Uom` preserves the invoice's sales UOM, including `LBS`. `PackSize` is descriptive evidence, never a multiplier inferred by the app.
- `PricingBasis` is `Case`, `Each`, `Pound`, or unresolved. `PricingQuantity` must equal shipped quantity for Case/Each, or the printed `Weight` for Pound. `WeightUom` is normalized to `LB` for pound calculations while the original line remains intact.
- Unknown values stay null. Out-of-stock text is retained in `OriginalText` and `FulfillmentStatus`; numeric shipped zero represents no shipment. The line remains in the extraction and line count.
- `ItemCode` and invoice numbers are strings, preserving leading zeros. `SourcePage` refers to the uploaded PDF's actual one-based page index. `ReadingConcerns` and `CodingReason` make uncertainty and proposed coding visible.
- Price precision is preserved as decimal; do not truncate a per-pound price to cents before multiplication. Current calculations round line amounts to cents away from zero. Supplier package-level rounding is not yet implemented; discrepancies remain blocking.

## Document evidence

Actual uploaded page count and scan detection come from PdfPig, not AI or imported invoice JSON. `DeclaredPageCount`, `PrintedPageLabels` and `Completeness` represent source readings. A mismatch or uncertainty blocks saving, even when line sums reconcile. Printed totals distinguish Final/Estimated/Unknown; tax/other charges are separate and require explicit confirmation. Conditional boilerplate fees are not charged invoice rows.

Use vision for scanned PDFs, including mixed text/image PDFs. OpenAI receives the full PDF through Responses file inputs and a strict structured schema. Human source comparison is still required. There is no local OCR engine; manual local JSON import is available without an API key. The adapter was tested with synthetic responses, not a live provider extraction run.

## Coding and review

Classify actual items individually, not supplier sections or filenames. Food-service disposables use SC6111 + DNGHL; reusable food-service utensils use SC6112 + DNGHL; beverage and food codes remain distinct. The confirmed laundry/stain-removal destainer uses SC6115 + DNGHL, including when a handwritten DHLIN annotation is present. That exception is a programmatic rule as well as extraction guidance.

`HistoricalReferences` stores handwritten requisition-like identifiers without treating them as invoice numbers, verified Workday records, or permission to create anything. Current payment status remains unknown unless independently established; historical amount-due/net terms are insufficient.

All extraction/import/correction resets `IndependentlyChecked`. Recording a human comparison requires a checkbox and reviewer/evidence notes; it does not override validation, completeness, duplicate checks or development-only restrictions. This local review record is not authenticated enterprise audit evidence. Old records remain readable; newly required fields stay unresolved until reviewed rather than being guessed.

## Draft boundary

The mock gateway rejects reference-only records. Normal mock saves require a source check, actual page count, no validation issues and no local duplicates. Live Workday duplicate checks, supplier/coding ID validation and draft creation are not implemented. Weight-based Workday mapping and unshipped-line treatment must be resolved before any future live adapter is allowed to act.
