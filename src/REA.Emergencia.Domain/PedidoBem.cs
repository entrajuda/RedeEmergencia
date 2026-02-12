using System;
using System.ComponentModel.DataAnnotations;

namespace REA.Emergencia.Domain;

public sealed class PedidoBem
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Localidade { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Freguesia { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Concelho { get; set; } = string.Empty;

    [MaxLength(100)]
    public string IdentificationNumber { get; set; } = string.Empty;

    public int Age { get; set; }

    public int HouseholdSize { get; set; }

    public int ChildrenUnder12 { get; set; }

    public int Youth13To17 { get; set; }

    public int Adults18Plus { get; set; }

    public int Seniors65Plus { get; set; }

    public bool ReceivesFoodSupport { get; set; }

    [MaxLength(200)]
    public string? FoodSupportInstitutionName { get; set; }

    public bool CanPickUpNearby { get; set; }

    [MaxLength(1000)]
    public string? Suggestions { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
