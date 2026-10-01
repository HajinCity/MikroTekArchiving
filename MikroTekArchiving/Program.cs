using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MikroTekArchiving.Data;
using MikroTekArchiving.Services;
using MikroTekArchiving.Services.Routers;

var builder = WebApplication.CreateBuilder(args);

// Lets the published app run as a Windows Service (does nothing when started normally).
builder.Host.UseWindowsService();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database: LocalDB while developing (appsettings.json),
// SQL Server Express on the server PC (appsettings.Production.json).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Router: we don't have a MikroTik yet, so use the simulated one.
// Later: builder.Services.AddSingleton<IRouterService, MikrotikRouterService>();
builder.Services.AddSingleton<IRouterService, FakeRouterService>();

// Voucher rules + background job that expires vouchers every 30 seconds.
builder.Services.AddScoped<VoucherService>();
builder.Services.AddHostedService<VoucherExpiryWorker>();

// Keep the login/form keys in App_Data so users stay signed in and forms keep working
// after the server restarts (otherwise every restart logs everybody out).
builder.Services.AddDataProtection()
    .SetApplicationName("MikroTekArchiving")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

// Scanned DV packets can be big (one sample packet is 29 MB), so allow large uploads.
var maxUploadBytes = builder.Configuration.GetValue("Uploads:MaxMegabytes", 200L) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = maxUploadBytes);

var app = builder.Build();

// Create/upgrade the database and add starter data (admin, plans, test vouchers).
// If the database can't be reached, keep the site running and explain why in the log
// instead of crashing (the portal pages still open; hotspot pages need the database).
var dbReady = false;
try
{
    await DbSeeder.SeedAsync(app.Services);
    dbReady = true;
}
catch (Exception ex)
{
    app.Logger.LogError(ex,
        "Cannot open the database. Check ConnectionStrings:DefaultConnection in appsettings{Env}.json " +
        "and that SQL Server is running on this PC. Connection string in use: {Conn}",
        app.Environment.IsProduction() ? ".Production" : "",
        builder.Configuration.GetConnectionString("DefaultConnection"));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// On the school LAN we serve plain http://SERVER-IP:5000, so the https redirect is
// switched off in appsettings.Production.json ("Hosting:UseHttpsRedirection": false).
if (app.Configuration.GetValue("Hosting:UseHttpsRedirection", true))
{
    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Quick check from any PC: http://SERVER-IP:5000/health
app.MapGet("/health", async (AppDbContext db) =>
{
    bool canConnect;
    try { canConnect = await db.Database.CanConnectAsync(); } catch { canConnect = false; }
    return Results.Json(new { status = "running", server = Environment.MachineName, database = canConnect ? "ok" : "not reachable", time = DateTime.Now });
});

// Disbursement Archiving Portal: /Portal/{controller}/{action}
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// When the server is ready, print the addresses the other PCs should open.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    foreach (var address in addresses)
    {
        var uri = new Uri(address.Replace("://*", "://0.0.0.0").Replace("://+", "://0.0.0.0").Replace("://[::]", "://0.0.0.0"));
        if (uri.Host is "0.0.0.0")
        {
            foreach (var ip in LanAddresses())
                app.Logger.LogInformation("Other PCs can open: {Scheme}://{Ip}:{Port}/Portal", uri.Scheme, ip, uri.Port);
        }
        app.Logger.LogInformation("On this PC open: {Scheme}://localhost:{Port}/Portal", uri.Scheme, uri.Port);
    }
    if (!dbReady) app.Logger.LogWarning("Started WITHOUT a database connection - see the error above.");
});

app.Run();

// IPv4 addresses of this PC's active network cards (Wi-Fi / Ethernet), skipping loopback.
static IEnumerable<string> LanAddresses() =>
    NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(a => a.Address.ToString())
        .Distinct();
