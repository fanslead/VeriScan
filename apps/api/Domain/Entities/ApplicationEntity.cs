namespace VeriScan.Domain.Entities;

public sealed class ApplicationEntity
{
    private ApplicationEntity()
    {
    }

    public ApplicationEntity(Guid tenantId, string publicId, string name, string environmentName)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        PublicId = publicId;
        Name = name;
        EnvironmentName = environmentName;
        Status = ApplicationStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string PublicId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string EnvironmentName { get; private set; } = string.Empty;

    public ApplicationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<ApplicationApiKey> ApiKeys { get; private set; } = new List<ApplicationApiKey>();

    public ICollection<ModerationRequest> ModerationRequests { get; private set; } = new List<ModerationRequest>();

    public void Rename(string name)
    {
        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        Status = ApplicationStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = ApplicationStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = ApplicationStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum ApplicationStatus
{
    Active,
    Suspended,
    Archived
}
