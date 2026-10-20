namespace CineManagement.Modules.Shared.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "MinIO"; // "MinIO" or "CloudflareR2"
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string BucketName { get; set; } = "cinemanagement-media";
    public string AccessKeyId { get; set; } = "minioadmin";
    public string SecretAccessKey { get; set; } = "minioadmin123";
    public string PublicBaseUrl { get; set; } = "http://localhost:9000/cinemanagement-media";
    public bool ForcePathStyle { get; set; } = true;
}
