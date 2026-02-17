using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instituicoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoEA = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConcelhoId = table.Column<int>(type: "int", nullable: true),
                    DistritoId = table.Column<int>(type: "int", nullable: true),
                    ZinfId = table.Column<int>(type: "int", nullable: true),
                    PessoaContacto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Telemovel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodigoPostalNumero = table.Column<int>(type: "int", nullable: true),
                    Localidade = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instituicoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instituicoes_CodigosPostais_CodigoPostalNumero",
                        column: x => x.CodigoPostalNumero,
                        principalTable: "CodigosPostais",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Instituicoes_Concelhos_ConcelhoId",
                        column: x => x.ConcelhoId,
                        principalTable: "Concelhos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Instituicoes_Distritos_DistritoId",
                        column: x => x.DistritoId,
                        principalTable: "Distritos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Instituicoes_Zinfs_ZinfId",
                        column: x => x.ZinfId,
                        principalTable: "Zinfs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Instituicoes_CodigoEA",
                table: "Instituicoes",
                column: "CodigoEA",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instituicoes_CodigoPostalNumero",
                table: "Instituicoes",
                column: "CodigoPostalNumero");

            migrationBuilder.CreateIndex(
                name: "IX_Instituicoes_ConcelhoId",
                table: "Instituicoes",
                column: "ConcelhoId");

            migrationBuilder.CreateIndex(
                name: "IX_Instituicoes_DistritoId",
                table: "Instituicoes",
                column: "DistritoId");

            migrationBuilder.CreateIndex(
                name: "IX_Instituicoes_ZinfId",
                table: "Instituicoes",
                column: "ZinfId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Instituicoes");
        }
    }
}
