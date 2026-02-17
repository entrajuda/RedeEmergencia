using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZinfEntityAndConcelhoZinfFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZINFId",
                table: "Concelhos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Zinfs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zinfs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Zinfs",
                columns: new[] { "Id", "Nome" },
                values: new object[,]
                {
                    { 1, "01- BACF LISBOA" },
                    { 2, "02- BACF PORTO" },
                    { 3, "03- BACF ÉVORA" },
                    { 4, "04- BACF COIMBRA" },
                    { 5, "05- BACF AVEIRO" },
                    { 6, "06- BACF ABRANTES" },
                    { 7, "07- BACF SETÚBAL" },
                    { 8, "08- BACF S.MIGUEL" },
                    { 9, "09- BACF C. DA BEIRA" },
                    { 10, "10- BACF LEIRIA-FAT." },
                    { 11, "11- BACF OESTE" },
                    { 12, "12- BACF ALGARVE" },
                    { 13, "13- BACF PORTALEGRE" },
                    { 14, "14- BACF BRAGA" },
                    { 15, "15- BACF SANTARÉM" },
                    { 16, "16- BACF VISEU" },
                    { 17, "17- BACF VIANA DO CASTELO" },
                    { 18, "18- BACF BEJA" },
                    { 19, "19- BACF TERCEIRA" },
                    { 20, "20- BACF MADEIRA" },
                    { 21, "21- BACF CASTELO BRANCO" },
                    { 22, "AÇORES" },
                    { 23, "NORDESTE" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Concelhos_ZINFId",
                table: "Concelhos",
                column: "ZINFId");

            migrationBuilder.CreateIndex(
                name: "IX_Zinfs_Nome",
                table: "Zinfs",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Concelhos_Zinfs_ZINFId",
                table: "Concelhos",
                column: "ZINFId",
                principalTable: "Zinfs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Concelhos_Zinfs_ZINFId",
                table: "Concelhos");

            migrationBuilder.DropTable(
                name: "Zinfs");

            migrationBuilder.DropIndex(
                name: "IX_Concelhos_ZINFId",
                table: "Concelhos");

            migrationBuilder.DropColumn(
                name: "ZINFId",
                table: "Concelhos");
        }
    }
}
