using EventCalendar.Data;
using EventCalendar.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<GameTemplateCatalog>();
builder.Services.AddSingleton<EventRepository>();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

await app.Services.GetRequiredService<EventRepository>().InitializeAsync();
app.Run();

public partial class Program;
