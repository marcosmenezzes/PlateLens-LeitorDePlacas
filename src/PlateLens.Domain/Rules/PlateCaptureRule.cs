namespace PlateLens.Domain.Rules;

/// <summary>Caixa de detecção expressa como proporção da largura e altura do quadro.</summary>
public readonly record struct NormalizedBox(double X, double Y, double Width, double Height)
{
    private const double Epsilon = 1e-9;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool IsValid => X >= -Epsilon && X <= 1 + Epsilon && Y >= -Epsilon && Y <= 1 + Epsilon &&
                           Width > 0 && Width <= 1 + Epsilon && Height > 0 && Height <= 1 + Epsilon &&
                           X + Width <= 1 + Epsilon && Y + Height <= 1 + Epsilon;
}

/// <summary>Reúne as condições mínimas para uma detecção poder gerar captura.</summary>
public static class PlateCaptureRule
{
    /// <summary>Valida região, caixa, confiança e formato brasileiro da placa.</summary>
    public static bool ShouldCapture(GateRegion region, NormalizedBox detectedPlate, string? ocrText, double confidence, double minimumConfidence, out string normalizedPlate)
    {
        normalizedPlate = string.Empty;
        return region.IsValid && detectedPlate.IsValid && confidence >= minimumConfidence &&
               region.Intersects(detectedPlate) &&
               PlateNumberRule.TryNormalize(ocrText, out normalizedPlate);
    }
}
