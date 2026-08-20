using System.Text.Json.Serialization;
using PlateLens.WebApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Registra banco, casos de uso, integração com visão e proteções da API.
builder.Services.AddPlateLens(builder.Configuration);

// Descobre os controllers e representa enums como nomes legíveis no JSON.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Define a ordem do tratamento de erros, CORS, limite de uso e endpoints.
app.UsePlateLensPipeline();

// Garante que o SQLite esteja pronto antes de aceitar a primeira requisição.
await app.InitializeDatabaseAsync();
app.Run();

// Permite que verificações de integração referenciem o ponto de entrada da API.
public partial class Program;
