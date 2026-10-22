using EventCalendar.Data;
using EventCalendar.Models;
using EventCalendar.Services;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace EventCalendar.Pages.Events;

public sealed class DetailsModel(EventRepository repository, GameTemplateCatalog catalog) : PageModel
{
    public GameEvent Event { get; private set; } = null!;
    public GameTemplate Template { get; private set; } = null!;
    public string RegistrationUrl { get; private set; } = "";
    public string QrSvg { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var gameEvent = await repository.GetEventAsync(id);
        if (gameEvent is null) return NotFound();
        Event = gameEvent;
        Template = catalog.Find(gameEvent.GameTemplateId)!;
        RegistrationUrl = Url.PageLink("/Events/Register", values: new { id })!;
        using var qrData = QRCodeGenerator.GenerateQrCode(RegistrationUrl, QRCodeGenerator.ECCLevel.Q);
        QrSvg = new SvgQRCode(qrData).GetGraphic(5);
        return Page();
    }

    public async Task<IActionResult> OnGetCalendarAsync(long id)
    {
        var gameEvent = await repository.GetEventAsync(id);
        if (gameEvent is null) return NotFound();
        var calendar = new Ical.Net.Calendar();
        calendar.Events.Add(new CalendarEvent
        {
            Summary = gameEvent.Name,
            Location = gameEvent.Location,
            Description = $"{gameEvent.Format} event. Registration: {Url.PageLink("/Events/Register", values: new { id })}",
            DtStart = new CalDateTime(gameEvent.StartLocal),
            DtEnd = new CalDateTime(gameEvent.EndLocal),
            Uid = $"event-{id}@tabletop-calendar.local"
        });
        var bytes = System.Text.Encoding.UTF8.GetBytes(new CalendarSerializer().SerializeToString(calendar));
        return File(bytes, "text/calendar; charset=utf-8", $"event-{id}.ics");
    }
}
