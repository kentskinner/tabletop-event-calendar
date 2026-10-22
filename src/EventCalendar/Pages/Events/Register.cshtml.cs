using System.ComponentModel.DataAnnotations;
using EventCalendar.Data;
using EventCalendar.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventCalendar.Pages.Events;

public sealed class RegisterModel(EventRepository repository) : PageModel
{
    public GameEvent Event { get; private set; } = null!;
    [BindProperty, Required, StringLength(80, MinimumLength = 2)] public string PlayerName { get; set; } = "";
    public RegistrationResult? Result { get; private set; }
    [TempData] public int? ConfirmedSeatNumber { get; set; }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();
        Result = await repository.RegisterAsync(id, PlayerName);
        if (Result.Status == RegistrationStatus.Confirmed)
        {
            ConfirmedSeatNumber = Result.SeatNumber;
            return RedirectToPage(new { id });
        }
        await LoadAsync(id);
        return Page();
    }

    private async Task<bool> LoadAsync(long id)
    {
        var gameEvent = await repository.GetEventAsync(id);
        if (gameEvent is null) return false;
        Event = gameEvent;
        return true;
    }
}
