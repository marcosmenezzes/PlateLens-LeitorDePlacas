namespace PlateLens.WebApi.Models;

public sealed record AnalyticsPeriod(DateOnly From, DateOnly To);
public sealed record AnalyticsSummary(int Entries, int Exits, int Inside, int Unknown, int Total);
public sealed record TimelinePoint(string Label, int Entries, int Exits, int Count);
public sealed record NamedCount(string Name, int Count);
public sealed record FrequentVehicle(string Plate, string Name, int Count);
public sealed record AverageStay(string Name, double Hours);
public sealed record RecognitionMetrics(int PlatesDetected, int OcrValid, int OcrInvalid, double RecognitionRate,
    double AverageDetectionConfidence, double AverageOcrConfidence, double AverageProcessingMs, int Rejected);
public sealed record CameraMetric(Guid Id, string Name, bool Online);
public sealed record AnalyticsResponse(AnalyticsPeriod Period, AnalyticsSummary Summary,
    IReadOnlyList<TimelinePoint> Daily, IReadOnlyList<TimelinePoint> Hourly, IReadOnlyList<NamedCount> ByType,
    IReadOnlyList<FrequentVehicle> FrequentVehicles, IReadOnlyList<AverageStay> AverageStayByType,
    RecognitionMetrics Recognition, IReadOnlyList<CameraMetric> Cameras, IReadOnlyList<AccessEventResponse> RecentEvents,
    string PeakHour);
