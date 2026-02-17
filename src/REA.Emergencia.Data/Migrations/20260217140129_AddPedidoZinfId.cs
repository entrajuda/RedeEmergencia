using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REA.Emergencia.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoZinfId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZinfId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE p
SET p.ZinfId = c.ZINFId
FROM dbo.Pedidos p
INNER JOIN dbo.TiposPedido tp ON tp.Id = p.TipoPedidoId
INNER JOIN dbo.PedidosBens pb ON pb.Id = p.ExternalRequestID
INNER JOIN dbo.Concelhos c ON c.Concelho = pb.Concelho
WHERE tp.TableName = 'PedidosBens'
  AND c.ZINFId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ZinfId",
                table: "Pedidos",
                column: "ZinfId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Zinfs_ZinfId",
                table: "Pedidos",
                column: "ZinfId",
                principalTable: "Zinfs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Zinfs_ZinfId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_ZinfId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ZinfId",
                table: "Pedidos");
        }
    }
}
