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
    public List<SharePointRowPreview> SharePointRows { get; set; } = [];
    [TempData] public string? Notice { get; set; }
    public void OnGet(Guid? id) { Jobs=store.All(); Selected=Jobs.Find(j=>j.Id==id); if(Selected?.Invoice is {} i) { Issues=Rules.Validate(i,Selected.UploadedPageCount); SharePointRows=Rules.SharePointPreview(i); } if(Selected is {} s) Duplicates=Jobs.Where(j=>Rules.PossibleDuplicate(s,j)).ToList(); }
    public IActionResult OnGetDocument(Guid id) { var j=store.Get(id); return File(System.IO.File.ReadAllBytes(store.Document(id)),j.Mime,j.FileName); }
    public IActionResult OnGetValidation(Guid id)
    {
        var j=store.Get(id);
        var payload=new { j.Id,j.FileName,j.ReferenceOnly,j.UploadedPageCount,j.DocumentKind,j.Provider,Status=j.IndependentlyChecked ? "Human source check recorded; Workday unverified" : "UNVERIFIED — final source review remains in Workday before submission",j.ReviewEvidence,j.Invoice,DraftGroups=j.DraftGroups,SharePointRows=j.Invoice is {} previewInvoice ? Rules.SharePointPreview(previewInvoice) : [],Issues=j.Invoice is {} i ? Rules.Validate(i,j.UploadedPageCount) : ["No extraction"],ReviewWarnings=j.Invoice is {} warningInvoice ? Rules.ReviewWarnings(warningInvoice,j.UploadedPageCount) : [],Workday="MOCK ONLY; live duplicate check not performed",SharePoint="PREVIEW ONLY; no live list choices were checked or written" };
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
    public async Task<IActionResult> OnPostUploadAsync(List<IFormFile> documents, bool referenceOnly = false)
    {
        if(documents is null || documents.Count==0 || documents.Count>20) { Notice="Choose between 1 and 20 invoices."; return RedirectToPage(); }
        if(documents.Any(f=>f.Length<=0 || f.Length>10*1024*1024) || documents.Sum(f=>(long)f.Length)>50*1024*1024) { Notice="Each file must be up to 10 MB and the batch up to 50 MB."; return RedirectToPage(); }
        await Gate.WaitAsync(); try {
            var accepted=new List<Guid>(); var rejected=new List<string>();
            foreach(var document in documents) {
                using var buffer=new MemoryStream(); await document.CopyToAsync(buffer); var bytes=buffer.ToArray();
                var mime=bytes.AsSpan().StartsWith("%PDF-"u8)?"application/pdf":bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})?"image/png":bytes.AsSpan().StartsWith(new byte[]{255,216,255})?"image/jpeg":null;
                if(mime is null) { rejected.Add($"{Path.GetFileName(document.FileName)}: unsupported file type"); continue; }
                var hash=Convert.ToHexString(SHA256.HashData(bytes)); var existing=store.All().Find(j=>j.Hash==hash);
                if(existing is not null) { if(referenceOnly && !existing.ReferenceOnly){existing.ReferenceOnly=true;existing.Record(existing.State,"Marked development sample: creation prohibited.");store.Save(existing);} accepted.Add(existing.Id); rejected.Add($"{Path.GetFileName(document.FileName)}: duplicate opened from local history"); continue; }
                (int Pages,string Kind) info;
                try { info=DocumentInspection.Inspect(bytes,mime); } catch { rejected.Add($"{Path.GetFileName(document.FileName)}: invalid/locked document or unsupported page count"); continue; }
                var job=new Job {Hash=hash,FileName=Path.GetFileName(document.FileName),Mime=mime,ReferenceOnly=referenceOnly,UploadedPageCount=info.Pages,DocumentKind=info.Kind};
                job.Record("Uploaded","Original saved; extraction requires explicit action.");
                await System.IO.File.WriteAllBytesAsync(store.Document(job.Id),bytes); store.Save(job); accepted.Add(job.Id);
            }
            Notice=$"Batch complete: {accepted.Count} accepted/opened; {rejected.Count} skipped or existing. "+string.Join("; ",rejected);
            return accepted.Count>0 ? RedirectToPage(new{id=accepted[0]}) : RedirectToPage();
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
            if(j.ReferenceOnly || !j.UploadedPageCount.HasValue || j.Invoice is null || Rules.Validate(j.Invoice,j.UploadedPageCount).Count>0 || duplicates || Rules.CreationBlocked(j.State)) { Notice="Mock drafts blocked: development sample, validation issue, duplicate or existing save state needs attention."; return RedirectToPage(new{id}); }
            if(j.DraftGroups.Any(x=>x.State is "SaveIntent" or "SaveUncertain")) { Notice="A category draft outcome is uncertain. Reconcile it before starting more mock drafts."; return RedirectToPage(new{id}); }
            var planned=Rules.CategoryGroups(j.Invoice).Where(g=>!string.IsNullOrWhiteSpace(g.CodingPair)).ToList();
            if(planned.Count==0 || planned.Any(g=>g.Category is null || g.Subtotal is null)) { Notice="Every invoice line needs a recognized coding pair and amount before mock drafts can be prepared."; return RedirectToPage(new{id}); }
            if(j.DraftGroups.Count==0) {
                j.DraftGroups=planned.Select(g=>new DraftGroup {Id=Rules.StableDraftId(j.Id,g.CodingPair),CodingPair=g.CodingPair,PurchaseCategory=g.Category,Total=g.Subtotal!.Value,LineCount=g.Lines.Count,LineNumbers=g.Lines.Select(x=>x.Number).ToList()}).ToList();
                j.Record(j.State,"Category draft plan persisted. Source comparison not recorded; invoice remains UNVERIFIED."); store.Save(j);
            }
            foreach(var draft in j.DraftGroups.Where(x=>x.State=="NotCreated")) {
                draft.State="SaveIntent"; j.Record("DraftGroupsInProgress",$"MOCK intent persisted for category {draft.CodingPair}."); store.Save(j);
                try { var result=workday.SaveForLater(j,draft); Verify(j,draft,result); store.Save(j); }
                catch { draft.State="SaveUncertain"; j.Record("DraftGroupsUncertain",$"MOCK outcome uncertain for {draft.CodingPair}; reconcile before retry."); store.Save(j); break; }
            }
            if(j.DraftGroups.All(x=>x.State=="MockDraftVerified")) j.Record("MockDraftsVerified","All category mock drafts independently read back. No live Workday action occurred.");
            store.Save(j); return RedirectToPage(new{id});
        } finally { Gate.Release(); }
    }
    void Verify(Job j,DraftGroup draft,MockResult? created=null)
    {
        var r=workday.Read(j.Id,draft.Id); var i=j.Invoice!;
        if(r is null || (created is not null && r!=created) || r.Status!="Draft (MOCK)" || r.Total!=draft.Total || r.LineCount!=draft.LineCount || r.Supplier!=i.Supplier || r.CodingPair!=draft.CodingPair || !r.Number.StartsWith("MOCK-")) throw new InvalidDataException();
        draft.Result=r; draft.State="MockDraftVerified"; j.Record(j.State,$"Category mock draft {draft.CodingPair} read back and verified; source remains UNVERIFIED.");
    }
    void VerifyLegacy(Job j)
    {
        var r=workday.Read(j.Id); var i=j.Invoice!;
        if(r is null || r.Status!="Draft (MOCK)" || r.Total!=i.Total || r.LineCount!=i.Lines.Count || r.Supplier!=i.Supplier || !r.Number.StartsWith("MOCK-")) throw new InvalidDataException();
        j.Result=r; j.Record("MockDraftVerified","Legacy mock result read back and verified. No live Workday action occurred.");
    }
    public async Task<IActionResult> OnPostReconcileAsync(Guid id)
    {
        await Gate.WaitAsync(); try { var j=store.Get(id); if(j.DraftGroups.Count==0 && (j.State is "SaveUncertain" or "SaveIntent")) { try { VerifyLegacy(j); } catch { j.Record("SaveUncertain","No matching legacy mock result confirmed. Remains blocked; investigate manually."); } } else { foreach(var draft in j.DraftGroups.Where(x=>x.State is "SaveUncertain" or "SaveIntent")) { try { Verify(j,draft); } catch { draft.State="SaveUncertain"; j.Record("DraftGroupsUncertain",$"No matching mock result confirmed for {draft.CodingPair}. Remains blocked; investigate manually."); } } if(j.DraftGroups.All(x=>x.State=="MockDraftVerified")) j.Record("MockDraftsVerified","All category mock drafts independently read back. No live Workday action occurred."); else if(j.DraftGroups.All(x=>x.State is not ("SaveIntent" or "SaveUncertain"))) j.Record("NeedsReview","Unattempted category drafts are ready for a separate mock creation attempt after reconciliation."); } store.Save(j); return RedirectToPage(new{id}); } finally { Gate.Release(); }
    }
    public async Task<IActionResult> OnPostDemoAsync()
    {
        await Gate.WaitAsync(); try {
            var existing=store.All().Find(j=>j.Hash=="SYNTHETIC-DEMO-CATEGORIES"); if(existing is not null) return RedirectToPage(new{id=existing.Id});
            var j=new Job { Hash="SYNTHETIC-DEMO-CATEGORIES",FileName="Synthetic two-category invoice (no original)",Provider="Synthetic fixture",UploadedPageCount=1,DocumentKind="Synthetic fixture",Invoice=new("Example Supplier","DEMO-002","2026-11-02","USD","Unpaid",91.10m,[
                new(4,"EA","Example beverage",19.55m,78.20m,"4 EA Example beverage $19.55 $78.20","SC6110|DHBEV") {OrderedQuantity=4,ShippedQuantity=4,PricingBasis="Each",PricingQuantity=4,FulfillmentStatus="Shipped",SourcePage=1},
                new(3,"EA","Example food item",4.30m,12.90m,"3 EA Example food item $4.30 $12.90","SC6110|DNGHL") {OrderedQuantity=3,ShippedQuantity=3,PricingBasis="Each",PricingQuantity=3,FulfillmentStatus="Shipped",SourcePage=1}
            ],[]) {Completeness="Complete",DeclaredPageCount=1,PrintedPageLabels="Synthetic page 1",TotalKind="Final",Subtotal=91.10m,Tax=0,OtherCharges=0} };
            j.Record("NeedsReview","Synthetic two-category fixture; not extracted from an uploaded invoice."); store.Save(j); return RedirectToPage(new{id=j.Id});
        } finally { Gate.Release(); }
    }
}
