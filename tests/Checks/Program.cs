using WorkdayInvoiceAssistant;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
// Optional evaluation takes external records only. No source fixtures or results enter Git.
if(args.Length==3 && args[0]=="--evaluate")
{
    var manifest=JsonSerializer.Deserialize<List<EvaluationInput>>(File.ReadAllText(args[1]),Store.Json)!;
    var results=manifest.Select(entry=> {
        var invoice=JsonSerializer.Deserialize<Invoice>(File.ReadAllText(entry.InvoicePath),Store.Json)!;
        var info=DocumentInspection.Inspect(File.ReadAllBytes(entry.SourcePath),"application/pdf");
        return new {entry.Stage,entry.SourcePath,entry.InvoicePath,PageCount=info.Pages,DocumentKind=info.Kind,Status="UNVERIFIED — local transcription, independent check pending",LineCount=invoice.Lines.Count,LineSum=invoice.Lines.Sum(l=>l.ExtendedAmount??0),invoice.Total,Issues=Rules.Validate(invoice,info.Pages),Invoice=invoice};
    }).ToList();
    var target=Path.GetFullPath(args[2]);
    if(target.StartsWith(Path.GetFullPath(Environment.CurrentDirectory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Evaluation output must stay outside source repository.");
    File.WriteAllText(target,JsonSerializer.Serialize(results,Store.Json));
    Console.WriteLine($"Evaluated {results.Count} external records. All remain UNVERIFIED. No gateway called.");return;
}
int count=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++;}
Invoice valid=new("Supplier","INV-1","2026-10-29","USD","Unpaid",78.20m,[new(4,"EA","Beverage",19.55m,78.20m,"4 EA Beverage $19.55 $78.20","SC6110|DHBEV") {OrderedQuantity=4,ShippedQuantity=4,PricingBasis="Each",PricingQuantity=4,FulfillmentStatus="Shipped",SourcePage=1}],[]) {Completeness="Complete",TotalKind="Final",Tax=0,OtherCharges=0,DeclaredPageCount=1};
if(args.SequenceEqual(new[]{"--targeted-corrections"})) { TargetedCorrections.Run(valid); return; }
Check(Rules.Validate(valid).Count==0,"valid decimal invoice");
Check(Rules.Validate(valid with{Total=78.21m}).Any(x=>x.Contains("Line sum")),"one-cent total mismatch blocks");
Check(Rules.Validate(valid with{Lines=[valid.Lines[0] with{ShippedQuantity=3,PricingQuantity=3}]}).Any(x=>x.Contains("quantity ×")),"line arithmetic mismatch");
Check(Rules.Validate(valid with{PaymentStatus="Paid"}).Count>0,"paid receipt blocked");
Check(Rules.Validate(valid with{PaymentStatus="Unknown"}).Count>0,"unknown payment blocked");
Check(Rules.Validate(valid with{Lines=[valid.Lines[0] with{Coding="SC6110|DHLIN"}]}).Count>0,"unlocked coding blocked");
Check(Rules.Validate(valid with{Lines=[valid.Lines[0] with{Description="Deposit"}]}).Count>0,"deposit blocked");
Check(Rules.Validate(valid with{Lines=[valid.Lines[0] with{ShippedQuantity=decimal.MaxValue,PricingQuantity=decimal.MaxValue}]}).Count>0,"overflow fails closed");
Check(Rules.Validate(valid with{Concerns=["Unreadable quantity"]}).Count>0,"extraction concern blocks");
var zeroLine=valid.Lines[0] with{Description="Example returnable tank",UnitPrice=0,ExtendedAmount=0,Coding=null};
Check(Rules.Validate(valid with{Lines=[..valid.Lines,zeroLine]}).Any(x=>x.Contains("zero-dollar")),"zero-dollar line retained but flagged for review");
var packed=valid with{InvoiceNumber="000123",Total=12.34m,Lines=[new(1,"CS","Example produce 4/5LB",12.34m,12.34m,"1 CS Example produce 4/5LB $12.34 $12.34","SC6110|DNGHL") {OrderedQuantity=1,ShippedQuantity=1,PricingBasis="Case",PricingQuantity=1,FulfillmentStatus="Shipped",SourcePage=1}]};
Check(Rules.Validate(packed).Count==0,"pack size does not multiply ordered quantity");
Check(Rules.RequestDescription(packed.Lines[0]).Contains("4/5LB"),"original pack size preserved in request description");
Check(System.Text.Json.JsonSerializer.Deserialize<Invoice>(System.Text.Json.JsonSerializer.Serialize(packed),Store.Json)?.InvoiceNumber=="000123","leading zeros preserved in invoice number");
var weighted=packed with {Total=28.95m,Lines=[packed.Lines[0] with {Weight=12.34m,WeightUom="LB",PricingBasis="Pound",PricingQuantity=12.34m,UnitPrice=2.346m,ExtendedAmount=28.95m}]};
Check(!Rules.Validate(weighted).Any(x=>x.Contains("quantity ×")),"billable pounds used instead of one case; three-decimal price retained");
Check(Rules.Validate(weighted).Any(x=>x.Contains("mapping requires")),"weight Workday mapping remains blocked");
Check(Rules.Validate(weighted with {Lines=[weighted.Lines[0] with{Weight=null}]}).Any(x=>x.Contains("pricing basis")),"missing weight never inferred from pack");
Check(Rules.Validate(weighted with {Lines=[weighted.Lines[0] with{PricingQuantity=1}]}).Any(x=>x.Contains("pricing basis")),"case count cannot masquerade as priced pounds");
var outOfStock=packed with {Total=12.34m,Lines=[packed.Lines[0],packed.Lines[0] with{Description="Reusable serving spoon",OrderedQuantity=2,ShippedQuantity=0,PricingQuantity=0,ExtendedAmount=0,FulfillmentStatus="OutOfStock",Coding="SC6112|DNGHL"}]};
Check(outOfStock.Lines.Count==2 && Rules.Validate(outOfStock).Any(x=>x.Contains("unfulfilled")),"out-of-stock reusable utensil retained without a charge");
Check(Rules.Validate(packed with{DeclaredPageCount=2,PrintedPageLabels="Page 01 of 02"},1).Any(x=>x.Contains("missing pages")),"actual vs printed page count mismatch blocks");
Check(Rules.Validate(packed with{Completeness="Unknown"},1).Any(x=>x.Contains("completeness")),"unusual page labels cannot be silently cleared");
Check(Rules.Validate(packed with{TotalKind="Estimated"}).Any(x=>x.Contains("estimated")),"estimated invoice total blocked");
var chemical=packed with{Lines=[packed.Lines[0] with{Description="Cleaner destainer stainblaster power",Coding="SC6129|DHLIN"}]};
Check(Rules.ApplyConfirmedCoding(chemical).Lines[0].Coding=="SC6115|DNGHL","confirmed chemical overrides handwritten coding");
Check(Rules.Validate(chemical).Any(x=>x.Contains("confirmed laundry")),"wrong confirmed chemical coding blocked");
Check(!Rules.Validate(packed with{Lines=[packed.Lines[0] with{Description="Coffee beans"}]}).Any(x=>x.Contains("special charge")),"coffee is not misidentified as a fee");
Check(Rules.Validate(packed with{Total=13.34m,Tax=1}).Any(x=>x.Contains("accounting review")) && !Rules.Validate(packed with{Total=13.34m,Tax=1}).Any(x=>x.Contains("Line sum")),"tax reconciles separately but still needs review");
var reference=new Job{ReferenceOnly=true,Invoice=valid,IndependentlyChecked=true,UploadedPageCount=1};
Check(!Rules.CanSave(reference),"development sample blocked despite source check");
Check(!Rules.CanSave(new Job{Invoice=valid,UploadedPageCount=1}),"unverified extraction cannot save");
Check(Rules.CanSave(new Job{Invoice=valid,UploadedPageCount=1,IndependentlyChecked=true}),"checked valid normal record can simulate save");
var oldJson="{\"Supplier\":\"Legacy\",\"Lines\":[],\"Concerns\":[]}";
Check(JsonSerializer.Deserialize<Invoice>(oldJson,Store.Json) is not null,"legacy records remain readable");
var responseBody=JsonSerializer.Serialize(new{status="completed",output=new[]{new{type="message",content=new[]{new{type="output_text",text=JsonSerializer.Serialize(valid)}}}}});
var fake=new FakeOpenAi(responseBody);
var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"OpenAI:ApiKey","synthetic-test-token"},{"OpenAI:Model","synthetic-model"}}).Build();
var extracted=await new OpenAiExtractor(config,new HttpClient(fake)).Extract("%PDF-synthetic"u8.ToArray(),"application/pdf",CancellationToken.None);
Check(extracted.Lines.Count==1,"structured provider response accepted through adapter");
using(var sent=JsonDocument.Parse(fake.RequestBody!)) {
 var request=sent.RootElement;
 Check(request.GetProperty("input")[0].GetProperty("content")[1].GetProperty("type").GetString()=="input_file","scanned PDF sent as file for vision, not empty text");
 var lineSchema=request.GetProperty("text").GetProperty("format").GetProperty("schema").GetProperty("properties").GetProperty("Lines").GetProperty("items");
 Check(lineSchema.GetProperty("required").EnumerateArray().Any(x=>x.GetString()=="PricingBasis") && lineSchema.GetProperty("properties").TryGetProperty("OrderedQuantity",out _),"strict schema includes pricing basis and separate quantities");
}
bool refused=false;try{await new OpenAiExtractor(config,new HttpClient(new FakeOpenAi("{\"status\":\"incomplete\"}"))).Extract([1],"image/png",CancellationToken.None);}catch(InvalidDataException){refused=true;}
Check(refused,"incomplete provider output rejected");
var a=new Job{Hash="A",Invoice=valid}; var b=new Job{Hash="B",Invoice=valid with{InvoiceNumber="inv 1"}};
Check(Rules.PossibleDuplicate(a,b),"normalized invoice duplicate");
Check(!Rules.PossibleDuplicate(a,a),"self excluded");
Check(Rules.PossibleDuplicate(a,new Job{Hash="A"}),"file hash duplicate before extraction");
Check(Rules.CreationBlocked("SaveUncertain"),"uncertain creation blocked");
var root=Path.Combine(Path.GetTempPath(),"wia-checks-"+Guid.NewGuid());
using(var store=new Store(root,Path.Combine(root,"source"))){
 a.Record("Extracting","test");store.Save(a);b.Record("SaveIntent","test");store.Save(b);store.Recover();
 Check(store.Get(a.Id).State=="ExtractionInterrupted","interrupted extraction recoverable");
 Check(store.Get(b.Id).State=="SaveUncertain","interrupted save uncertain");
 var mock=new MockWorkdayGateway(store);mock.SaveForLater(a);var r=mock.Read(a.Id);
 bool sampleBlocked=false;try{mock.SaveForLater(reference);}catch(InvalidOperationException){sampleBlocked=true;}Check(sampleBlocked,"gateway itself refuses development samples");
 Check(r?.Total==78.20m && r.LineCount==1 && r.Status=="Draft (MOCK)","mock independent readback");
 bool blocked=false;try{mock.SaveForLater(a);}catch(IOException){blocked=true;}Check(blocked,"second mock creation refused");
}
using(var reopened=new Store(root,Path.Combine(root,"source"))) Check(reopened.Get(b.Id).State=="SaveUncertain","state survives reopen");
Console.WriteLine($"{count} checks passed. Synthetic records retained at {root}");
record EvaluationInput(string Stage,string SourcePath,string InvoicePath);
sealed class FakeOpenAi(string body) : HttpMessageHandler
{
 public string? RequestBody {get;private set;}
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {RequestBody=await request.Content!.ReadAsStringAsync(ct);return new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(body)};}
}

