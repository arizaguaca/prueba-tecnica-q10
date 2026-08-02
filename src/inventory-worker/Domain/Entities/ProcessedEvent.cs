namespace InventoryWorker.Domain.Entities;

public class ProcessedEvent
{
    public Guid EventId { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string Resultado { get; private set; } = string.Empty; // "Reserved" o "Rejected"
    public string? MotivoRechazo { get; private set; }
    public DateTime ProcesadoEn { get; private set; }

    // Required for EF Core
    private ProcessedEvent() { }

    public ProcessedEvent(Guid eventId, string tipoEvento, string resultado, string? motivoRechazo = null)
    {
        EventId = eventId;
        TipoEvento = tipoEvento;
        Resultado = resultado;
        MotivoRechazo = motivoRechazo;
        ProcesadoEn = DateTime.UtcNow;
    }
}
