using System.Globalization;

namespace WorkdayInvoiceAssistant;

public sealed record InvoiceLine(decimal? Quantity, string? Uom, string? Description,
    decimal? UnitPrice, decimal? ExtendedAmount, string? OriginalText, string? Coding)
{
    // Quantity also holds an explicitly printed single billed quantity when no ordered/shipped columns exist.
    public decimal? OrderedQuantity { get; init; }
    public decimal? ShippedQuantity { get; init; }
    public string? PricingBasis { get; init; }
    public decimal? PricingQuantity { get; init; }
    public decimal? Weight { get; init; }
    public string? WeightUom { get; init; }
    public string? PackSize { get; init; }
    public string? ItemCode { get; init; }
    public string? FulfillmentStatus { get; init; }
    public int? SourcePage { get; init; }
    public string? CodingReason { get; init; }
    public List<string> ReadingConcerns { get; init; } = [];
}
public sealed record Invoice(string? Supplier, string? InvoiceNumber, string? InvoiceDate,
    string? Currency, string? PaymentStatus, decimal? Total, List<InvoiceLine> Lines, List<string> Concerns)
{
    public int? DeclaredPageCount { get; init; }
    public string? PrintedPageLabels { get; init; }
    public string? Completeness { get; init; }
    public string? TotalKind { get; init; }
    public decimal? Subtotal { get; init; }
    public decimal? Tax { get; init; }
    public decimal? OtherCharges { get; init; }
    public List<string> HistoricalReferences { get; init; } = [];
}
public sealed record MockResult(string Number, string Status, string Supplier, decimal Total, int LineCount, string? CodingPair = null);
public sealed class DraftGroup
{
    public Guid Id { get; set; }
    public string CodingPair { get; set; } = "";
    public string? PurchaseCategory { get; set; }
    public decimal Total { get; set; }
    public int LineCount { get; set; }
    public List<int> LineNumbers { get; set; } = [];
    public string State { get; set; } = "NotCreated";
    public MockResult? Result { get; set; }
}
public sealed record SharePointFieldPreview(string Name, string? Value, bool Required, string Status);
public sealed record SharePointRowPreview(string CodingPair, string? Category, decimal? Subtotal, int LineCount, List<SharePointFieldPreview> Fields);
public sealed class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Hash { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Mime { get; set; } = "";
    public string State { get; set; } = "Uploaded";
    public string Provider { get; set; } = "";
    public bool ReferenceOnly { get; set; }
    public int? UploadedPageCount { get; set; }
    public string DocumentKind { get; set; } = "Not inspected";
    public bool IndependentlyChecked { get; set; }
    public string? ReviewEvidence { get; set; }
    public Invoice? Invoice { get; set; }
    public MockResult? Result { get; set; }
    public List<DraftGroup> DraftGroups { get; set; } = [];
    public List<string> Events { get; set; } = [];
    public void Record(string state, string message)
    { State = state; Events.Add($"{DateTimeOffset.UtcNow:O} | {state} | {message}"); }
}
public static class Rules
{
    public static readonly Dictionary<string,string> Coding = new()
    {
        ["SC6110|DHBEV"] = "Beverages", ["SC6110|DNGHL"] = "Food",
        ["SC6111|DNGHL"] = "Food Service Disposables", ["SC6112|DNGHL"] = "Food Service Supplies",
        ["SC6115|DNGHL"] = "Janitorial Supplies", ["SC6129|DHLIN"] = "General supplies (not otherwise specified)",
        ["SC6421|DNGHL"] = "Employee Development Services", ["SC6449|DNGHL"] = "Rent/Lease <= 1 year",
        ["SC6908|DNGHL"] = "Equipment Maintenance (not otherwise specified)"
    };
    public static List<string> Validate(Invoice i, int? uploadedPageCount = null)
    {
        List<string> issues = [];
        if (string.IsNullOrWhiteSpace(i.Supplier)) issues.Add("Supplier needs review; resolve the exact Workday supplier.");
        if (string.IsNullOrWhiteSpace(i.InvoiceNumber)) issues.Add("Invoice number is required.");
        if (!DateOnly.TryParseExact(i.InvoiceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) issues.Add("Invoice date must be YYYY-MM-DD.");
        if (i.Currency != "USD") issues.Add("Currency requires review; milestone supports USD only.");
        if (i.PaymentStatus != "Unpaid") issues.Add("Paid or unknown payment status: determine the appropriate transaction with BYU before proceeding.");
        if (i.Total is null or <= 0) issues.Add("A positive invoice total is required; credits require review.");
        if (i.TotalKind != "Final") issues.Add("Invoice total is estimated or unconfirmed; obtain the final invoice.");
        if (i.Completeness != "Complete") issues.Add("Document completeness is missing, uncertain or incomplete.");
        if (PageCountMismatch(i,uploadedPageCount) && !NicholasLabels(i)) issues.Add($"Possible missing pages: uploaded {uploadedPageCount}, printed count {i.DeclaredPageCount} ({i.PrintedPageLabels}).");
        if (i.Lines is null || i.Lines.Count == 0) { issues.Add("No invoice lines found."); return issues; }
        if (i.Lines.Count > 500) { issues.Add("More than 500 lines; manual review required."); return issues; }
        decimal sum = 0;
        try
        {
            for (int n = 0; n < i.Lines.Count; n++)
            {
                var l = i.Lines[n]; var prefix = $"Line {n+1}: ";
                if (l is null) { issues.Add(prefix + "missing line data."); continue; }
                bool billedOnly = l.OrderedQuantity is null && l.ShippedQuantity is null && l.PricingBasis is "Case" or "Each";
                if ((billedOnly ? l.Quantity is null or <= 0 : l.OrderedQuantity is null or < 0 || l.ShippedQuantity is null or < 0) || l.UnitPrice is null or < 0 || l.ExtendedAmount is null or < 0) issues.Add(prefix + "missing/invalid source quantities or amount; review required.");
                if (l.OrderedQuantity != l.ShippedQuantity) issues.Add(prefix + "ordered and shipped quantities differ; review fulfillment.");
                if ((!billedOnly && l.FulfillmentStatus != "Shipped") || l.FulfillmentStatus == "OutOfStock" || l.ShippedQuantity == 0) issues.Add(prefix + "unfulfilled or uncertain shipment; preserve this line and review treatment.");
                if (l.UnitPrice == 0 || l.ExtendedAmount == 0) issues.Add(prefix + "zero-dollar item: preserve the line and review its coding and transaction treatment.");
                decimal? basisQuantity = l.PricingBasis switch { "Case" when l.Uom == "CS" => billedOnly ? l.Quantity : l.ShippedQuantity, "Each" when l.Uom == "EA" => billedOnly ? l.Quantity : l.ShippedQuantity, "Pound" when l.WeightUom == "LB" => l.Weight, _ => null };
                if (basisQuantity is null or < 0 || (l.PricingQuantity != basisQuantity && !(billedOnly && l.PricingQuantity is null))) issues.Add(prefix + "pricing basis or quantity is unresolved/inconsistent; do not infer from pack size.");
                if (basisQuantity.HasValue && l.UnitPrice.HasValue && l.ExtendedAmount.HasValue && decimal.Round(basisQuantity.Value * l.UnitPrice.Value, 2, MidpointRounding.AwayFromZero) != l.ExtendedAmount) issues.Add(prefix + "pricing quantity × unit price does not match extended amount.");
                if (l.PricingBasis == "Pound") issues.Add(prefix + "weight pricing arithmetic uses pounds; Workday Each/quantity/price mapping requires explicit review before a draft.");
                if (l.SourcePage is null or < 1 || (uploadedPageCount.HasValue && l.SourcePage > uploadedPageCount)) issues.Add(prefix + "source page missing or outside uploaded pages.");
                issues.AddRange((l.ReadingConcerns ?? ["Missing reading concerns."]).Select(x=>prefix+x));
                if (string.IsNullOrWhiteSpace(l.Uom) || string.IsNullOrWhiteSpace(l.Description) || string.IsNullOrWhiteSpace(l.OriginalText)) issues.Add(prefix + "original UOM, description and complete source line are required.");
                if (!Coding.ContainsKey(l.Coding ?? "")) issues.Add(prefix + "choose a locked coding pair.");
                if (ConfirmedChemical(l) && l.Coding != "SC6115|DNGHL") issues.Add(prefix + "confirmed laundry/stain-removal chemical must use SC6115|DNGHL; handwritten DHLIN is not applicable.");
                if (ConfirmedNitrileGloves(l) && l.Coding != "SC6112|DNGHL") issues.Add(prefix + "confirmed nitrile food-service gloves must use SC6112|DNGHL.");
                var s = (l.Description ?? "").ToLowerInvariant();
                if (System.Text.RegularExpressions.Regex.IsMatch(s,@"\b(tax|deposit|credit|fee|freight|shipping|discount)\b")) issues.Add(prefix + "special charge requires review and is blocked in this milestone.");
                sum += l.ExtendedAmount ?? 0;
            }
            if (i.Subtotal.HasValue && sum != i.Subtotal) issues.Add($"Line sum {sum:0.00} does not match printed subtotal {i.Subtotal:0.00}.");
            if (sum + (i.Tax ?? 0) + (i.OtherCharges ?? 0) != i.Total) issues.Add($"Line sum plus tax/charges does not match invoice total {i.Total:0.00}. Do not insert balancing amounts.");
            if (i.Tax is null || i.OtherCharges is null) issues.Add("Tax/other charges not confirmed; use explicit zero only when supported by the document.");
            if (i.Tax != 0 || i.OtherCharges != 0) issues.Add("Tax or other charges require accounting review before a draft.");
        }
        catch (OverflowException) { issues.Add("Amounts exceed supported decimal range."); }
        issues.AddRange(i.Concerns ?? ["Missing extraction concerns; review required."]);
        return issues;
    }
    static string Normalize(string? s) => new((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    public static bool PossibleDuplicate(Job a, Job b) => a.Id != b.Id &&
        (a.Hash == b.Hash || (a.Invoice is {} x && b.Invoice is {} y &&
        ((!string.IsNullOrWhiteSpace(x.InvoiceNumber) && Normalize(x.InvoiceNumber) == Normalize(y.InvoiceNumber)) ||
        (Normalize(x.Supplier).Length > 0 && Normalize(x.Supplier) == Normalize(y.Supplier) && x.Total == y.Total && x.InvoiceDate == y.InvoiceDate))));
    public static bool CreationBlocked(string state) => state is "SaveIntent" or "SaveUncertain" or "MockDraftVerified" or "DraftGroupsInProgress" or "DraftGroupsUncertain" or "MockDraftsVerified";
    public static bool ConfirmedChemical(InvoiceLine l) => (l.Description ?? "").Contains("STAINBLASTER",StringComparison.OrdinalIgnoreCase) && (l.Description ?? "").Contains("DESTAINER",StringComparison.OrdinalIgnoreCase);
    public static bool ConfirmedNitrileGloves(InvoiceLine l) => System.Text.RegularExpressions.Regex.IsMatch(l.Description ?? "",@"\bGLOVES?\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase) && System.Text.RegularExpressions.Regex.IsMatch(l.Description ?? "",@"\b(NITRILE|NTRLE)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public static Invoice ApplyConfirmedCoding(Invoice invoice) => invoice with { Lines=invoice.Lines.Select(l=>ConfirmedChemical(l) ? l with {Coding="SC6115|DNGHL",CodingReason="Matt confirmed laundry/stain-removal chemical: Janitorial Supplies + DNGHL; ignore handwritten DHLIN."} : ConfirmedNitrileGloves(l) ? l with {Coding="SC6112|DNGHL",CodingReason="Matt confirmed disposable nitrile food-service gloves: Food Service Supplies + DNGHL."} : l).ToList() };
    static bool PageCountMismatch(Invoice i,int? actual) => actual.HasValue && i.DeclaredPageCount.HasValue && actual != i.DeclaredPageCount;
    static bool NicholasLabels(Invoice i) => (i.Supplier ?? "").StartsWith("Nicholas",StringComparison.OrdinalIgnoreCase);
    public static List<string> ReviewWarnings(Invoice i,int? actual) => PageCountMismatch(i,actual) && NicholasLabels(i) ? [$"Review ambiguous Nicholas page label '{i.PrintedPageLabels}': uploaded {actual}, recorded label count {i.DeclaredPageCount}. Label alone does not establish missing pages; confirm completeness separately."] : [];
    public static bool CanSave(Job job) => !job.ReferenceOnly && job.IndependentlyChecked && job.UploadedPageCount.HasValue && job.Invoice is {} i && !CreationBlocked(job.State) && Validate(i,job.UploadedPageCount).Count==0;
    public static List<(string CodingPair, string? Category, List<(int Number, InvoiceLine Line)> Lines, decimal? Subtotal)> CategoryGroups(Invoice invoice) =>
        invoice.Lines.Select((line,index)=>(Number:index+1,Line:line))
            .GroupBy(x=>x.Line.Coding ?? "")
            .Select(g=>(CodingPair:g.Key,Category:Coding.GetValueOrDefault(g.Key),Lines:g.ToList(),Subtotal:g.All(x=>x.Line.ExtendedAmount.HasValue) ? g.Sum(x=>x.Line.ExtendedAmount!.Value) : (decimal?)null))
            .OrderBy(g=>g.CodingPair,StringComparer.Ordinal).ToList();
    public static Guid StableDraftId(Guid jobId,string codingPair)
    {
        var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(jobId.ToString("N")+"|"+codingPair));
        return new Guid(hash.AsSpan(0,16));
    }
    public static List<SharePointRowPreview> SharePointPreview(Invoice invoice)
    {
        return CategoryGroups(invoice).Select(g=>new SharePointRowPreview(g.CodingPair,g.Category,g.Subtotal,g.Lines.Count,
        [
            new("Purchase Description",null,true,"Unresolved: confirm description format."),
            new("Transaction Date",invoice.InvoiceDate,true,"Proposed from invoice date; confirm date rule."),
            new("Total Cost",g.Subtotal?.ToString("0.00",CultureInfo.InvariantCulture),true,"Confirmed category subtotal."),
            new("Vendor",invoice.Supplier,true,"Proposed from invoice supplier; SharePoint choice lookup required."),
            new("Receipt/Order #",invoice.InvoiceNumber,false,"Proposed from invoice number."),
            new("Purchase Category",g.Category,true,g.Category is null ? "Unresolved coding pair." : "Candidate from coding map; verify against live SharePoint choices."),
            new("Class ID",null,false,"Unmapped optional field."),
            new("DH_Category",null,false,"Unmapped optional field."),
            new("Finance Category",null,false,"Unmapped optional field.")
        ])).ToList();
    }
    public static string RequestDescription(InvoiceLine l) => $"Ordered {l.OrderedQuantity}; shipped {l.ShippedQuantity} {l.Uom}; {l.Description}; pack {l.PackSize}; weight {l.Weight} {l.WeightUom}; price {l.UnitPrice:0.####}/{l.PricingBasis}; extended {l.ExtendedAmount:0.00} | Source: {l.OriginalText}";
}
