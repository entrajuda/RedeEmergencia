using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserZinf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserZinfs",
                columns: table => new
                {
                    UserPrincipalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ZinfId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserZinfs", x => new { x.UserPrincipalName, x.ZinfId });
                    table.ForeignKey(
                        name: "FK_UserZinfs_Zinfs_ZinfId",
                        column: x => x.ZinfId,
                        principalTable: "Zinfs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserZinfs_ZinfId",
                table: "UserZinfs",
                column: "ZinfId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserZinfs");
        }
    }
}
