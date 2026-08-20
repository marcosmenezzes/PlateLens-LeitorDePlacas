namespace PlateLens.Domain.Rules;

/// <summary>Representa a região de captura em coordenadas normalizadas de zero a um.</summary>
public readonly record struct GateRegion(double X, double Y, double Width, double Height)
{
    private const double Epsilon = 1e-9;
    public bool IsValid => X >= -Epsilon && X <= 1 + Epsilon && Y >= -Epsilon && Y <= 1 + Epsilon &&
                           Width > 0 && Width <= 1 + Epsilon && Height > 0 && Height <= 1 + Epsilon &&
                           X + Width <= 1 + Epsilon && Y + Height <= 1 + Epsilon;

    /// <summary>Informa se um ponto está dentro da região.</summary>
    public bool Contains(double centerX, double centerY) =>
        IsValid && centerX >= X && centerX <= X + Width && centerY >= Y && centerY <= Y + Height;

    /// <summary>Informa se uma detecção possui alguma interseção com a região.</summary>
    public bool Intersects(NormalizedBox box) => IsValid && box.IsValid &&
        box.X + box.Width >= X && box.X <= X + Width && box.Y + box.Height >= Y && box.Y <= Y + Height;

    /// <summary>Corrige números inválidos e limita a região aos limites do quadro.</summary>
    public static GateRegion Normalize(double x, double y, double width, double height)
    {
        x = Math.Clamp(double.IsFinite(x) ? x : .2, 0, .99);
        y = Math.Clamp(double.IsFinite(y) ? y : .25, 0, .99);
        width = Math.Clamp(double.IsFinite(width) ? width : .6, .01, 1 - x);
        height = Math.Clamp(double.IsFinite(height) ? height : .5, .01, 1 - y);
        return new(Math.Round(x, 9), Math.Round(y, 9), Math.Round(width, 9), Math.Round(height, 9));
    }
}
