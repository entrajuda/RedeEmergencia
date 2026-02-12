namespace REA.Emergencia.Web.Models;

public sealed class PedidoBemInputModel
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Localidade { get; set; } = string.Empty;
    public string Freguesia { get; set; } = string.Empty;
    public string Concelho { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public int Age { get; set; }
    public int HouseholdSize { get; set; }
    public int ChildrenUnder12 { get; set; }
    public int Youth13To17 { get; set; }
    public int Adults18Plus { get; set; }
    public int Seniors65Plus { get; set; }
    public bool? ReceivesFoodSupport { get; set; }
    public string? FoodSupportInstitutionName { get; set; }
    public bool? CanPickUpNearby { get; set; }
    public string? Suggestions { get; set; }
}
