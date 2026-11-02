using WorkdayInvoiceAssistant;

static class TargetedCorrections
{
    public static void Run(Invoice valid)
    {
        int count=0;
        void Check(bool ok,string name) { if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++; }
        var billed=valid.Lines[0] with {OrderedQuantity=null,ShippedQuantity=null,PricingQuantity=null,FulfillmentStatus="Unknown"};
        var invoice=valid with {Lines=[billed]};
        Check(Rules.Validate(invoice).Count==0,"single explicit billed quantity accepted");
        foreach(decimal? q in new decimal?[]{null,0,-1}) Check(Rules.Validate(invoice with{Lines=[billed with{Quantity=q}]}).Count>0,"missing/nonpositive billed quantity blocked");
        Check(Rules.Validate(invoice with{Lines=[billed with{UnitPrice=19.56m}]}).Any(x=>x.Contains("quantity ×")),"billed arithmetic mismatch blocked");
        Check(Rules.Validate(invoice with{Lines=[billed with{PricingQuantity=3}]}).Any(x=>x.Contains("pricing basis")),"conflicting pricing quantity blocked");
        var tanks=invoice with {Lines=[billed,billed with{Quantity=1,Description="CO2 tank",UnitPrice=0,ExtendedAmount=0,Coding=null},billed with{Quantity=2,Description="CO2 tank",UnitPrice=0,ExtendedAmount=0,Coding=null}]};
        var issues=Rules.Validate(tanks);
        Check(tanks.Lines.Count==3 && issues.Count(x=>x.Contains("zero-dollar"))==2 && issues.Count(x=>x.Contains("coding pair"))==2,"both zero-charge tanks retained with coding review");
        var glove=valid.Lines[0] with{Description="GLOVE, NTRLE X-LARGE PF BLK",Coding="SC6111|DNGHL"};
        var corrected=Rules.ApplyConfirmedCoding(valid with{Lines=[glove,valid.Lines[0]]});
        Check(corrected.Lines[0].Coding=="SC6112|DNGHL" && corrected.Lines[1]==valid.Lines[0],"confirmed nitrile coding applied without unrelated changes");
        Check(Rules.Validate(valid with{Lines=[glove]}).Any(x=>x.Contains("confirmed nitrile")) && !Rules.Validate(corrected).Any(x=>x.Contains("confirmed nitrile")),"obsolete glove code blocked; confirmed code accepted");
        var nicholas=valid with{Supplier="Nicholas and Company",Completeness="Unknown",DeclaredPageCount=21,PrintedPageLabels="Page 1 of 21"};
        Check(Rules.ReviewWarnings(nicholas,2).Count==1 && !Rules.Validate(nicholas,2).Any(x=>x.Contains("missing pages")),"ambiguous Nicholas label warns without asserting missing pages");
        Check(Rules.Validate(nicholas,2).Any(x=>x.Contains("completeness")),"unknown required pages still block completeness");
        Check(Rules.Validate(nicholas with{Completeness="Complete"},2).Count==0,"established completeness leaves label warning nonblocking");
        Check(Rules.Validate(valid with{DeclaredPageCount=2},1).Any(x=>x.Contains("missing pages")),"unambiguous page mismatch still blocks");
        Console.WriteLine($"{count} targeted checks passed.");
    }
}
