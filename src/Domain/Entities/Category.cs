namespace Domain.Entities;

public class Category {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
}
