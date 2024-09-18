/// <summary>
/// Used as an intermediary between external an external calendar and what we will use to build up bandwidth actions
/// </summary>
public class CalendarItem
{
    public required string Id { get; set; }
    public required int Count { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public required string Location { get; set; }
    public string? Subject { get; set; }
}