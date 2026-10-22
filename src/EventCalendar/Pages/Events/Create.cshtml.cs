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
        EventCreationResult? creation = null;
        if (template is null)
            ModelState.AddModelError("Input.GameTemplateId", "Choose a valid game.");
        else
        {
            creation = template.CreateEvent(Input.Name.Trim(), Input.Format, Input.StartLocal,
                Input.Capacity, Input.Location.Trim());
            foreach (var error in creation.Errors)
                ModelState.AddModelError($"Input.{error.Key}", error.Value);
        }

        if (!ModelState.IsValid || creation?.Event is null) return Page();
        var id = await repository.CreateEventAsync(creation.Event);
        return RedirectToPage("Details", new { id });
    }
}
