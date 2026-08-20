namespace PlateLens.Domain.Entities;

public class RecognitionAttempt : BaseEntity
{
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string? NormalizedPlate { get; set; }
    public string PlateType { get; set; } = string.Empty;
    public bool FormatValid { get; set; }
    public bool InsideRegion { get; set; }
    public bool Accepted { get; set; }
    public bool EventCreated { get; set; }
    public double DetectionConfidence { get; set; }
    public double OcrConfidence { get; set; }
    public double ProcessingMs { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? TrackingId { get; set; }
}
