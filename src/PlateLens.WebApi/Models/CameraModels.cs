namespace PlateLens.WebApi.Models;

public record RegisterCameraRequest(string Name, string IpAddress, int Port = 554);
public record UpdateGateRegionRequest(double X, double Y, double Width, double Height);
