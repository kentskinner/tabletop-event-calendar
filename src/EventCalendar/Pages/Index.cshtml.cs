using EventCalendar.Data;
using EventCalendar.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventCalendar.Pages;

public sealed class IndexModel(EventRepository repository) : PageModel
{
    public IReadOnlyList<IGrouping<DateOnly, GameEvent>> Days { get; private set; } = [];

    public async Task OnGetAsync() => Days = (await repository.ListEventsAsync())
        .GroupBy(x => DateOnly.FromDateTime(x.StartLocal)).ToList();
}
