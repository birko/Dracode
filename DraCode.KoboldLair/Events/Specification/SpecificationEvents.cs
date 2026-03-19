namespace DraCode.KoboldLair.Events.Specification;

/// <summary>
/// Event data DTOs for specification domain events.
/// Serialized as JSON in DomainEvent.EventData.
/// </summary>

public static class SpecificationEventTypes
{
    public const string Created = "SpecificationCreated";
    public const string Updated = "SpecificationUpdated";
    public const string Approved = "SpecificationApproved";
    public const string FeatureAdded = "FeatureAdded";
    public const string FeatureModified = "FeatureModified";
    public const string FeatureRemoved = "FeatureRemoved";
    public const string FeatureStatusChanged = "FeatureStatusChanged";
}

public class SpecificationCreatedData
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

public class SpecificationUpdatedData
{
    public string Content { get; set; } = string.Empty;
    public string PreviousContentHash { get; set; } = string.Empty;
    public string NewContentHash { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? ChangeDescription { get; set; }
}

public class SpecificationApprovedData
{
    public string ApprovedBy { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class FeatureAddedData
{
    public string FeatureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
}

public class FeatureModifiedData
{
    public string FeatureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? PreviousDescription { get; set; }
    public string? PreviousPriority { get; set; }
}

public class FeatureRemovedData
{
    public string FeatureId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class FeatureStatusChangedData
{
    public string FeatureId { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
}
