using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
using System.Text.Json;
namespace WorkdayInvoiceAssistant.Pages;
public class IndexModel(Store store, IInvoiceExtractor extractor, IWorkdayGateway workday) : PageModel
{
    static readonly SemaphoreSlim Gate = new(1,1);
    public List<Job> Jobs { get; set; } = [];
    public Job? Selected { get; set; }
    public List<string> Issues { get; set; } = [];
    public List<Job> Duplicates { get; set; } = [];
    [TempData] public string? Notice { get; set; }
    public void OnGet(Guid? id) { Jobs=store.All(); Selected=Jobs.Find(j=>j.Id==id); if(Selected?.Invoice is {} i) Issues=Rules.Validate(i,Selected.UploadedPageCount); if(Selected is {} s) Duplicates=Jobs.Where(j=>Rules.PossibleDuplicate(s,j)).ToList(); }
    public IActionResult OnGetDocument(Guid id) { var j=store.Get(id); return File(System.IO.File.ReadAllBytes(store.Document(id)),j.Mime,j.FileName); }
    public IActionResult OnGetValidation(Guid id)
    {
        var j=store.Get(id);
        var payload=new { j.Id,j.FileName,j.ReferenceOnly,j.UploadedPageCount,j.DocumentKind,j.Provider,Status=j.IndependentlyChecked ? "Human source check recorded; Workday unverified" : "UNVERIFIED — independent source check required",j.ReviewEvidence,j.Invoice,Issues=j.Invoice is {} i ? Rules.Validate(i,j.UploadedPageCount) : ["No extraction"],ReviewWarnings=j.Invoice is {} warningInvoice ? Rules.ReviewWarnings(warningInvoice,j.UploadedPageCount) : [],Workday="MOCK ONLY; live duplicate check not performed" };
        return File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload,Store.Json)),"application/json","validation-"+id+".json");
    }
    public async Task<IActionResult> OnPostImportAsync(Guid id,IFormFile? extraction)
    {
        if(extraction is null || extraction.Length>2*1024*1024){Notice="Choose an invoice JSON file up to 2 MB.";return RedirectToPage(new{id});}
        using var reader=new StreamReader(extraction.OpenReadStream());
        return await OnPostReviewAsync(id,await reader.ReadToEndAsync());
    }
    public async Task<IActionResult> OnPostVerifyAsync(Guid id,bool checkedSource,string evidence)
    {
        await Gate.WaitAsync();try {
            var j=store.Get(id);
            if(!checkedSource || string.IsNullOrWhiteSpace(evidence) || j.Invoice is null || Rules.CreationBlocked(j.State)){Notice="Record an independent source comparison and evidence first.";return RedirectToPage(new{id});}
            j.IndependentlyChecked=true;j.ReviewEvidence=evidence.Trim();j.Record(j.State,"Human source comparison recorded; validation issues still apply; not Workday verification.");store.Save(j);return RedirectToPage(new{id});
        }finally{Gate.Release();}
    }
    public async Task<IActionResult> OnPostUploadAsync(IFormFile? document, bool referenceOnly = false)
    {
        if(document is null || document.Length is <=0 or >10*1024*1024) { Notice="Choose a PDF, PNG or JPEG up to 10 MB."; return RedirectToPage(); }
        using var buffer=new MemoryStream(); await document.CopyToAsync(buffer); var bytes=buffer.ToArray();
        var mime=bytes.AsSpan().StartsWith("%PDF-"u8)?"application/pdf":bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})?"image/png":bytes.AsSpan().StartsWith(new byte[]{255,216,255})?"image/jpeg":null;
        if(mime is null) { Notice="Unsupported file signature. Use PDF, PNG or JPEG."; return RedirectToPage(); }
        await Gate.WaitAsync(); try {
            var hash=Convert.ToHexString(SHA256.HashData(bytes)); var existing=store.All().Find(j=>j.Hash==hash);
            if(existing is not null) { if(referenceOnly && !existing.ReferenceOnly){existing.ReferenceOnly=true;existing.Record(existing.State,"Marked development sample: creation prohibited.");store.Save(existing);} Notice="Identical document already exists; opened existing record. No new processing started."; return RedirectToPage(new{id=existing.Id}); }
            (int Pages,string Kind) info;
            try { info=DocumentInspection.Inspect(bytes,mime); } catch { Notice="Document could not be inspected. Use an unlocked, valid PDF (1–50 pages) or image."; return RedirectToPage(); }
            var job=new Job {Hash=hash,FileName=Path.GetFileName(document.FileName),Mime=mime,ReferenceOnly=referenceOnly,UploadedPageCount=info.Pages,DocumentKind=info.Kind};
            job.Record("Uploaded","Original saved; extraction requires explicit action.");
            await System.IO.File.WriteAllBytesAsync(store.Document(job.Id),bytes); store.Save(job);
            return RedirectToPage(new{id=job.Id});
        } finally { Gate.Release(); }
    }
    public async Task<IActionResult> OnPostExtractAsync(Guid id, bool consent)
    {
        await Gate.WaitAsync(); try {
            var j=store.Get(id);
            if(!consent || j.State is not ("Uploaded" or "ExtractionFailed" or "ExtractionInterrupted")) { Notice="Extraction requires consent and an uploaded or interrupted record."; return RedirectToPage(new{id}); }
            j.Provider="OpenAI"; j.Record("Extracting","Explicit extraction requested."); store.Save(j);
            try { j.Invoice=await extractor.Extract(await System.IO.File.ReadAllBytesAsync(store.Document(id)),j.Mime,HttpContext.RequestAborted); j.IndependentlyChecked=false; j.ReviewEvidence=null; j.Record("NeedsReview","Extraction received; UNVERIFIED pending independent source check."); }
            catch (InvalidOperationException e) { j.Record("ExtractionFailed",e.Message); }
            catch { j.Record("ExtractionFailed","Extraction failed or was interrupted. No data accepted. Explicit retry available."); }
            store.Save(j); return RedirectToPage(new{id});
        } finally { Gate.Release(); }
    }
    public async Task<IActionResult> OnPostReviewAsync(Guid id, string invoiceJson)
    {
        await Gate.WaitAsync(); try {
            var j=store.Get(id); if(Rules.CreationBlocked(j.State)) { Notice="Saved or uncertain records are locked."; return RedirectToPage(new{id}); }
            try { var i=JsonSerializer.Deserialize<Invoice>(invoiceJson,Store.Json); if(i?.Lines is null || i.Concerns is null || i.Lines.Any(l=>l is null) || i.Lines.Count>500) throw new JsonException(); j.Invoice=Rules.ApplyConfirmedCoding(i); if(string.IsNullOrEmpty(j.Provider))j.Provider="Manual transcription/import (not API extraction)"; j.IndependentlyChecked=false;j.ReviewEvidence=null; j.Record("NeedsReview","Review data saved; UNVERIFIED. Original and prior revisions retained."); store.Save(j); }
            catch { Notice="Invalid invoice JSON. Nothing changed."; }
            return RedirectToPage(new{id});
        } finally { Gate.Release(); }
    }
    public async Task<IActionResult> OnPostMockAsync(Guid id)
    {
        await Gate.WaitAsync(); try {
            var j=store.Get(id);
            var duplicates=store.All().Any(x=>Rules.PossibleDuplicate(j,x));
            if(j.ReferenceOnly || !j.UploadedPageCount.HasValue || j.Invoice is null || Rules.Validate(j.Invoice,j.UploadedPageCount).Count>0 || duplicates || Rules.CreationBlocked(j.State)) { Notice="Mock draft blocked: development sample, validation issue, duplicate or existing save state needs attention."; return RedirectToPage(new{id}); }
            j.Record(j.State,"Mock draft requested after automated validation and local duplicate checks. Source comparison not recorded; invoice remains UNVERIFIED.");
            store.Save(j);
            j.Record("SaveIntent","MOCK Save for Later intent persisted."); store.Save(j);
            try { workday.SaveForLater(j); Verify(j); }
            catch { j.Record("SaveUncertain","MOCK save outcome uncertain; use reconciliation, never create again."); }
            store.Save(j); return RedirectToPage(new{id});
        } finally { Gate.Release(); }
    }
    void Verify(Job j)
    {
        var r=workday.Read(j.Id); var i=j.Invoice!;
        if(r is null || r.Status!="Draft (MOCK)" || r.Total!=i.Total || r.LineCount!=i.Lines.Count || r.Supplier!=i.Supplier || !r.Number.StartsWith("MOCK-")) throw new InvalidDataException();
        j.Result=r; j.Record("MockDraftVerified","MOCK result read back and verified. No live Workday action occurred.");
    }
    public async Task<IActionResult> OnPostReconcileAsync(Guid id)
    {
        await Gate.WaitAsync(); try { var j=store.Get(id); if(j.State is "SaveUncertain" or "SaveIntent") { try { Verify(j); } catch { j.Record("SaveUncertain","No matching mock result confirmed. Remains blocked; investigate manually."); } store.Save(j); } return RedirectToPage(new{id}); } finally { Gate.Release(); }
    }
    public async Task<IActionResult> OnPostDemoAsync()
    {
        await Gate.WaitAsync(); try {
            var existing=store.All().Find(j=>j.Hash=="SYNTHETIC-DEMO"); if(existing is not null) return RedirectToPage(new{id=existing.Id});
            var j=new Job { Hash="SYNTHETIC-DEMO",FileName="Synthetic example (no original)",Provider="Synthetic fixture",UploadedPageCount=1,DocumentKind="Synthetic fixture",Invoice=new("Example Supplier","DEMO-001","2026-10-29","USD","Unpaid",78.20m,[new(4,"EA","Example beverage",19.55m,78.20m,"4 EA Example beverage $19.55 $78.20","SC6110|DHBEV") {OrderedQuantity=4,ShippedQuantity=4,PricingBasis="Each",PricingQuantity=4,FulfillmentStatus="Shipped",SourcePage=1}],[]) {Completeness="Complete",DeclaredPageCount=1,PrintedPageLabels="Synthetic page 1",TotalKind="Final",Subtotal=78.20m,Tax=0,OtherCharges=0} };
            j.Record("NeedsReview","Synthetic fixture; not extracted from an uploaded invoice."); store.Save(j); return RedirectToPage(new{id=j.Id});
        } finally { Gate.Release(); }
    }
}
