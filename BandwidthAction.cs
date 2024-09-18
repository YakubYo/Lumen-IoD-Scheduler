public class BandwidthAction
{
    public required string ServiceId { get; set; }
    public required DateTime Time { get; set; }
    public required string Bandwidth { get; set; }
    public required int Priority { get; set; }
    public required string CalendarItemId { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? MasterSiteId { get; set; }
    public string? PartnerId { get; set; }
    public string? Status { get; set; }

}