using Microsoft.EntityFrameworkCore;
using PlateLens.Infra.Data;

namespace PlateLens.WebApi.Configuration;

/// <summary>Prepara o banco local antes que a API comece a receber requisições.</summary>
public static class DatabaseInitializer
{
    /// <summary>Cria o schema inicial e mantém compatibilidade com bancos anteriores ao histórico de reconhecimento.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        // ponytail: compatibilidade com o banco criado via EnsureCreated; adotar migrations antes de distribuir atualizações de schema.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RecognitionAttempts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RecognitionAttempts" PRIMARY KEY,
                "CameraId" TEXT NOT NULL,
                "OccurredAt" TEXT NOT NULL,
                "RawText" TEXT NOT NULL,
                "NormalizedPlate" TEXT NULL,
                "PlateType" TEXT NOT NULL,
                "FormatValid" INTEGER NOT NULL,
                "InsideRegion" INTEGER NOT NULL,
                "Accepted" INTEGER NOT NULL,
                "EventCreated" INTEGER NOT NULL,
                "DetectionConfidence" REAL NOT NULL,
                "OcrConfidence" REAL NOT NULL,
                "ProcessingMs" REAL NOT NULL,
                "RejectionReason" TEXT NULL,
                "TrackingId" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                CONSTRAINT "FK_RecognitionAttempts_Cameras_CameraId" FOREIGN KEY ("CameraId") REFERENCES "Cameras" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_RecognitionAttempts_CameraId" ON "RecognitionAttempts" ("CameraId");
            CREATE INDEX IF NOT EXISTS "IX_RecognitionAttempts_OccurredAt" ON "RecognitionAttempts" ("OccurredAt");
            """);
    }
}
