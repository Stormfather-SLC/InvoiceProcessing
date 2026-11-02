using System.Text.Json;
namespace WorkdayInvoiceAssistant;

// One local process owns the directory. Atomic replacement plus immutable originals.
public sealed class Store : IDisposable
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string Root { get; }
    private readonly FileStream ownership;
    public Store(string root, string sourceRoot)
    {
        Root = Path.GetFullPath(root);
        var source = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((Root + Path.DirectorySeparatorChar).StartsWith(source, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Data must be outside the source tree.");
        Directory.CreateDirectory(Root);
        ownership = new FileStream(Path.Combine(Root, ".owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public string Document(Guid id) => Path.Combine(Root, id + ".document");
    public List<Job> All() => Directory.GetFiles(Root, "*.json").Select(p => JsonSerializer.Deserialize<Job>(File.ReadAllText(p), Json) ?? throw new InvalidDataException("Invalid history record.")).OrderByDescending(x => x.Events.LastOrDefault()).ToList();
    public Job Get(Guid id) => JsonSerializer.Deserialize<Job>(File.ReadAllText(Path.Combine(Root,id+".json")), Json) ?? throw new InvalidDataException();
    public void Save(Job job)
    {
        var path = Path.Combine(Root, job.Id + ".json"); var temp = path + ".tmp";
        if (File.Exists(path))
        {
            var revisions = Path.Combine(Root, "revisions");
            Directory.CreateDirectory(revisions);
            File.Copy(path, Path.Combine(revisions, job.Id + "-" + Guid.NewGuid() + ".json"));
        }
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, job, Json); stream.Flush(true); }
        File.Move(temp, path, true);
    }
    public void Recover()
    {
        foreach (var job in All())
        {
            if (job.State == "Extracting") { job.Record("ExtractionInterrupted", "Interrupted extraction; explicit retry is available."); Save(job); }
            if (job.State == "SaveIntent") { job.Record("SaveUncertain", "Reconcile saved result before any creation attempt."); Save(job); }
            var changed=false;
            foreach(var draft in job.DraftGroups.Where(x=>x.State=="SaveIntent")) { draft.State="SaveUncertain"; changed=true; }
            if(changed) { job.Record("DraftGroupsUncertain","Interrupted category draft write marked uncertain; reconcile before any retry."); Save(job); }
        }
    }
    public void Dispose() => ownership.Dispose();
}
