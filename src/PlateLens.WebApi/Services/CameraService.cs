using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Models;

namespace PlateLens.WebApi.Services;

/// <summary>Executa os casos de uso de cadastro, seleção, região e exclusão de câmeras.</summary>
public class CameraService(AppDbContext db)
{
    /// <summary>Lista as câmeras com a fonte ativa primeiro.</summary>
    public Task<List<Camera>> ListAsync(CancellationToken cancellationToken) =>
        db.Cameras.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    /// <summary>Valida uma câmera IPv4 privada, cadastra-a e torna-a a fonte ativa.</summary>
    public async Task<Camera> RegisterAsync(RegisterCameraRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64) throw new ArgumentException("Informe um nome de até 64 caracteres.");
        if (!CameraNetworkPolicy.TryNormalizePrivateIpv4(request.IpAddress, out var ip)) throw new ArgumentException("Use somente um IPv4 privado da rede local.");
        if (request.Port is < 1 or > 65535) throw new ArgumentException("Informe uma porta entre 1 e 65535.");
        if (await db.Cameras.AnyAsync(x => x.IpAddress == ip, cancellationToken)) throw new InvalidOperationException("Já existe uma câmera com esse IP.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Cameras.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        var camera = new Camera { Name = name, SourceKind = CameraSourceKind.Network, IpAddress = ip, Port = request.Port, IsActive = true };
        db.Cameras.Add(camera);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return camera;
    }

    /// <summary>Seleciona exatamente uma câmera como fonte ativa.</summary>
    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Cameras.AnyAsync(x => x.Id == id, cancellationToken)) throw new KeyNotFoundException("Câmera não encontrada.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Cameras.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, x => x.Id == id), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Normaliza e persiste a região editável de captura da câmera.</summary>
    public async Task UpdateRegionAsync(Guid id, UpdateGateRegionRequest request, CancellationToken cancellationToken)
    {
        var region = GateRegion.Normalize(request.X, request.Y, request.Width, request.Height);
        var camera = await db.Cameras.FindAsync([id], cancellationToken) ?? throw new KeyNotFoundException("Câmera não encontrada.");
        camera.RegionX = region.X; camera.RegionY = region.Y; camera.RegionWidth = region.Width; camera.RegionHeight = region.Height;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Remove uma câmera de rede e reativa a câmera nativa quando necessário.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await db.Cameras.FindAsync([id], cancellationToken) ?? throw new KeyNotFoundException("Câmera não encontrada.");
        if (camera.SourceKind == CameraSourceKind.Native) throw new InvalidOperationException("A câmera nativa não pode ser removida.");
        db.Cameras.Remove(camera);
        if (camera.IsActive)
        {
            var native = await db.Cameras.FindAsync([AppDbContext.NativeCameraId], cancellationToken);
            if (native is not null) native.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
