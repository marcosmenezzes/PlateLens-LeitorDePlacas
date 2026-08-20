namespace PlateLens.Domain.Entities;

public enum VehicleType { Passeio, Caminhonete, Caminhao, Carreta, Desconhecido }

public class Vehicle : BaseEntity
{
    public string Plate { get; set; } = string.Empty;
    public string Name { get; set; } = "Desconhecido";
    public VehicleType VehicleType { get; set; } = VehicleType.Desconhecido;
    public ICollection<AccessEvent> AccessEvents { get; set; } = [];
}
