using System.ComponentModel.DataAnnotations;

namespace EXAMENPARCIAL.Models;

public class Incidencia
{
    public int Id { get; set; }

    [Required]
    public string Estacion { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    public int Prioridad { get; set; }

    public string Estado { get; set; } = "Abierta";
}
