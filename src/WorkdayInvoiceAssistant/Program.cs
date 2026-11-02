using WorkdayInvoiceAssistant;
var builder = WebApplication.CreateBuilder(args);
// This loopback app normally runs as Production; explicitly load local user-secrets too.
builder.Configuration.AddUserSecrets<OpenAiExtractor>(optional:true);
builder.WebHost.UseUrls("http://127.0.0.1:5187");
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=55*1024*1024);
builder.Services.AddRazorPages();
builder.Services.AddSingleton(new Store(builder.Configuration["DataRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorkdayInvoiceAssistant"), Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath,"../.."))));
builder.Services.AddSingleton<IWorkdayGateway,MockWorkdayGateway>();
builder.Services.AddHttpClient<IInvoiceExtractor,OpenAiExtractor>(h=>h.Timeout=TimeSpan.FromMinutes(3));
var app = builder.Build();
app.Use(async (context,next)=> {
    if (context.Request.Host.Host is not ("127.0.0.1" or "localhost")) { context.Response.StatusCode=400; return; }
    context.Response.Headers["Content-Security-Policy"]="default-src 'self'; style-src 'self'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["Cache-Control"]="no-store";
    try { await next(); } catch { if (!context.Response.HasStarted) { context.Response.StatusCode=500; await context.Response.WriteAsync("Operation stopped. Local records are preserved. Check configuration and processing history; do not repeat an uncertain save."); } }
});
app.Services.GetRequiredService<Store>().Recover();
app.UseStaticFiles(); app.MapRazorPages(); app.Run();
