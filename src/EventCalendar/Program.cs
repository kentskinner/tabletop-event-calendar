using EventCalendar.Data;
using EventCalendar.Services;

var seedDemoData = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);
args = args.Where(arg => !string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase)).ToArray();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<GameTemplateCatalog>();
builder.Services.AddSingleton<EventRepository>();
builder.Services.AddSingleton<DemoDataSeeder>();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

await app.Services.GetRequiredService<EventRepository>().InitializeAsync();
if (seedDemoData)
    await app.Services.GetRequiredService<DemoDataSeeder>().SeedAsync();
app.Run();

public partial class Program;
