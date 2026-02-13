using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoBemProductTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NeededProductTypes",
                table: "PedidosBens",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OtherNeededProductTypesDetails",
                table: "PedidosBens",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeededProductTypes",
                table: "PedidosBens");

            migrationBuilder.DropColumn(
                name: "OtherNeededProductTypesDetails",
                table: "PedidosBens");
        }
    }
}
