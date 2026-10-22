using EventCalendar.Services;
using Xunit;

namespace EventCalendar.Tests;

public sealed class GameTemplateTests
{
    private readonly GameTemplateCatalog _catalog = new();

    [Fact]
    public void CreateEvent_RejectsFormatFromAnotherTemplate()
    {
        var magic = _catalog.Find("magic")!;

        var result = magic.CreateEvent("Wrong format", "Sealed", DateTime.Now, 8, "Store");

        Assert.False(result.IsSuccess);
        Assert.Contains("Format", result.Errors.Keys);
    }

    [Fact]
    public void CreateEvent_RejectsCapacityAboveTemplateMaximum()
    {
        var lorcana = _catalog.Find("lorcana")!;

        var result = lorcana.CreateEvent("Too large", "Sealed", DateTime.Now, 25, "Store");

        Assert.False(result.IsSuccess);
        Assert.Contains("Capacity", result.Errors.Keys);
    }
}
