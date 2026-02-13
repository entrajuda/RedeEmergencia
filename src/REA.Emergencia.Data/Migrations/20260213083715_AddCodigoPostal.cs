using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoPostal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodigosPostais",
                columns: table => new
                {
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Freguesia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConcelhoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosPostais", x => x.Numero);
                    table.CheckConstraint("CK_CodigosPostais_Numero_Range", "[Numero] >= 1000000 AND [Numero] <= 9000000");
                    table.ForeignKey(
                        name: "FK_CodigosPostais_Concelhos_ConcelhoId",
                        column: x => x.ConcelhoId,
                        principalTable: "Concelhos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosPostais_ConcelhoId",
                table: "CodigosPostais",
                column: "ConcelhoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosPostais");
        }
    }
}
