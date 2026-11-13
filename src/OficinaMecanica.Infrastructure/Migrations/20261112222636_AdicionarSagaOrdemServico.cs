using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSagaOrdemServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SagasOrdemServico",
                schema: "GestaoOrdemServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoPreparacao = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    PreparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrcamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersaoOrcamento = table.Column<int>(type: "int", nullable: false),
                    ValorOrcamento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Decisao = table.Column<int>(type: "int", nullable: true),
                    InicioFisicoConfirmado = table.Column<bool>(type: "bit", nullable: false),
                    FinalizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreparacaoCancelada = table.Column<bool>(type: "bit", nullable: false),
                    OrcamentoCancelado = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagasOrdemServico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SagasOrdemServico_OrdensServico_OrdemServicoId",
                        column: x => x.OrdemServicoId,
                        principalSchema: "GestaoOrdemServico",
                        principalTable: "OrdensServico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagasOrdemServico_OrdemServicoId",
                schema: "GestaoOrdemServico",
                table: "SagasOrdemServico",
                column: "OrdemServicoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagasOrdemServico",
                schema: "GestaoOrdemServico");
        }
    }
}
