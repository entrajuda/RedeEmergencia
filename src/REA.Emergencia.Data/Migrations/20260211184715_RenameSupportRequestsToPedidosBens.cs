using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSupportRequestsToPedidosBens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SupportRequests",
                table: "SupportRequests");

            migrationBuilder.RenameTable(
                name: "SupportRequests",
                newName: "PedidosBens");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PedidosBens",
                table: "PedidosBens",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PedidosBens",
                table: "PedidosBens");

            migrationBuilder.RenameTable(
                name: "PedidosBens",
                newName: "SupportRequests");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupportRequests",
                table: "SupportRequests",
                column: "Id");
        }
    }
}
