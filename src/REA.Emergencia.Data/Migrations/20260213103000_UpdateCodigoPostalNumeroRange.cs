using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations;

public partial class UpdateCodigoPostalNumeroRange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CodigosPostais_Numero_Range",
            table: "CodigosPostais");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CodigosPostais_Numero_Range",
            table: "CodigosPostais",
            sql: "[Numero] >= 1000000 AND [Numero] <= 9999999");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CodigosPostais_Numero_Range",
            table: "CodigosPostais");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CodigosPostais_Numero_Range",
            table: "CodigosPostais",
            sql: "[Numero] >= 1000000 AND [Numero] <= 9000000");
    }
}
