namespace Domain.Entities;

public class SiteVisit {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public string Page { get; set; } = default!;
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}
