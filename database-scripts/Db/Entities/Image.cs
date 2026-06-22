namespace Db.Entities;

public class Image : BaseEntity
{
    public Guid? TenantsId { get; set; }
    public Tenant? Tenant { get; set; }
    public required string EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public required string StorageKey { get; set; }
    public string? OriginalName { get; set; }
    public int SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int SortOrder { get; set; }
    public Guid? UploadedByUsersId { get; set; }
    public string? UploaderType { get; set; }
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public string? ContentType { get; set; }
    public string? Checksum { get; set; }
    public required string Tag { get; set; } = "Generic";
}
