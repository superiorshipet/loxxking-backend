namespace Domain.Entities;

public class Country {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string DefaultLanguage { get; set; } = "en";
}
