namespace PlateLens.Domain.Rules;

/// <summary>Combina leituras próximas no tempo para reduzir erros ocasionais do OCR.</summary>
public sealed class PlateConsensusTracker(TimeSpan? window = null, int requiredReads = 3)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(2.5);
    private readonly Dictionary<Guid, List<PlateRead>> _reads = [];
    private readonly object _lock = new();

    /// <summary>Acumula uma leitura ponderada e devolve a placa quando existe consenso suficiente.</summary>
    public string? Observe(Guid cameraId, string plate, double confidence, double quality, DateTime now)
    {
        lock (_lock)
        {
            if (!_reads.TryGetValue(cameraId, out var reads)) _reads[cameraId] = reads = [];
            reads.RemoveAll(read => now - read.At > _window);
            var next = new PlateRead(plate, Math.Max(.01, confidence * Math.Clamp(quality, .1, 1)), now);
            if (reads.Count > 0 && reads[^1].At == now)
            {
                if (next.Weight > reads[^1].Weight) reads[^1] = next;
            }
            else
            {
                if (reads.Count > 0 && Distance(reads[^1].Plate, plate) > 2) reads.Clear();
                reads.Add(next);
            }
            if (reads.Count >= 2 && reads[^1].Plate == reads[^2].Plate)
            {
                _reads.Remove(cameraId);
                return plate;
            }
            if (reads.Count < requiredReads) return null;

            var totalWeight = reads.Sum(read => read.Weight);
            var characters = new char[7];
            for (var index = 0; index < characters.Length; index++)
            {
                var winner = reads.GroupBy(read => read.Plate[index])
                    .Select(group => new { Character = group.Key, Weight = group.Sum(read => read.Weight) })
                    .MaxBy(candidate => candidate.Weight)!;
                if (winner.Weight / totalWeight < .55) return null;
                characters[index] = winner.Character;
            }

            var result = new string(characters);
            if (!PlateNumberRule.TryNormalize(result, out result)) return null;
            _reads.Remove(cameraId);
            return result;
        }
    }

    /// <summary>Remove leituras pendentes associadas a uma placa excluída.</summary>
    public void Forget(string plate)
    {
        lock (_lock)
            foreach (var cameraId in _reads.Where(item => item.Value.Any(read => read.Plate == plate)).Select(item => item.Key).ToArray())
                _reads.Remove(cameraId);
    }

    /// <summary>Conta quantas posições diferem entre duas placas de sete caracteres.</summary>
    private static int Distance(string left, string right) => left.Zip(right).Count(pair => pair.First != pair.Second);
    private sealed record PlateRead(string Plate, double Weight, DateTime At);
}
