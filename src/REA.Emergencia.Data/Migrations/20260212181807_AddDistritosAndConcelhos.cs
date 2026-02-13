using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDistritosAndConcelhos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Distritos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Distrito = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distritos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Concelhos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Concelho = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DistritoId = table.Column<int>(type: "int", nullable: false),
                    ZINF = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concelhos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Concelhos_Distritos_DistritoId",
                        column: x => x.DistritoId,
                        principalTable: "Distritos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concelhos_DistritoId",
                table: "Concelhos",
                column: "DistritoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Concelhos");

            migrationBuilder.DropTable(
                name: "Distritos");
        }
    }
}
