using System.ComponentModel.DataAnnotations;
using EventCalendar.Data;
using EventCalendar.Models;
using EventCalendar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventCalendar.Pages.Events;

public sealed class CreateModel(GameTemplateCatalog catalog, EventRepository repository) : PageModel
{
    public IReadOnlyList<GameTemplate> Templates => catalog.All;

    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required, StringLength(100)] public string Name { get; set; } = "";
        [Required] public string GameTemplateId { get; set; } = "magic";
        [Required] public string Format { get; set; } = "Standard";
        [Required] public DateTime StartLocal { get; set; } = DateTime.Today.AddDays(1).AddHours(18);
        [Range(1, 30)] public int Capacity { get; set; } = 16;
        [Required, StringLength(120)] public string Location { get; set; } = "Downtown Game Store";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var template = catalog.Find(Input.GameTemplateId);
        if (template is null)
            ModelState.AddModelError("Input.GameTemplateId", "Choose a valid game.");
        else
        {
            if (!template.Formats.Contains(Input.Format)) ModelState.AddModelError("Input.Format", "Choose a format offered by this game.");
            if (Input.Capacity > template.MaxCapacity) ModelState.AddModelError("Input.Capacity", $"{template.Name} events support at most {template.MaxCapacity} players.");
            if (Input.Capacity < template.MinimumPlayers) ModelState.AddModelError("Input.Capacity", $"Capacity must allow at least {template.MinimumPlayers} players.");
        }

        if (!ModelState.IsValid || template is null) return Page();
        var id = await repository.CreateEventAsync(new(0, Input.Name.Trim(), template.Id, Input.Format,
            Input.StartLocal, template.DefaultDurationMinutes, Input.Capacity, template.MinimumPlayers, Input.Location.Trim()));
        return RedirectToPage("Details", new { id });
    }
}
