using System.ComponentModel.DataAnnotations;

namespace InventoryWorker.Infrastructure.Configuration;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
