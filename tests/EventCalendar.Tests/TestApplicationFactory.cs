using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EventCalendar.Tests;

internal sealed class TestApplicationFactory(
    string databasePath,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Events"] = $"Data Source={databasePath};Default Timeout=10"
            };
            if (settings is not null)
                foreach (var setting in settings)
                    values[setting.Key] = setting.Value;
            configuration.AddInMemoryCollection(values);
        });
}
