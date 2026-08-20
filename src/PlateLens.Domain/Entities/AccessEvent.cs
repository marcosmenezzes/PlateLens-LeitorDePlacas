namespace PlateLens.Domain.Entities;

public enum AccessEventType { Entry, Exit }

public class AccessEvent : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
    public string PlateDetected { get; set; } = string.Empty;
    public AccessEventType EventType { get; set; }
    public DateTime OccurredAt { get; set; }
    public double Confidence { get; set; }
}
