namespace PlateLens.Domain.Entities;

public enum CameraSourceKind { Native, Network }

public class Camera : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public CameraSourceKind SourceKind { get; set; }
    public int? DeviceIndex { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public bool IsActive { get; set; }
    public double RegionX { get; set; } = .2;
    public double RegionY { get; set; } = .25;
    public double RegionWidth { get; set; } = .6;
    public double RegionHeight { get; set; } = .5;
}
