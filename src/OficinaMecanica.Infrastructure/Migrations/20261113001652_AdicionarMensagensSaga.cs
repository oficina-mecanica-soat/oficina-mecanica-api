using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMensagensSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxSaga",
                schema: "GestaoOrdemServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersaoNegocio = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Corpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublicadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Responsavel = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseAte = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxSaga", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecebimentosMensagem",
                schema: "GestaoOrdemServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChaveOperacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecebimentosMensagem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxSaga_PublicadoEm",
                schema: "GestaoOrdemServico",
                table: "OutboxSaga",
                column: "PublicadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxSaga_Tipo_OrdemServicoId_VersaoNegocio",
                schema: "GestaoOrdemServico",
                table: "OutboxSaga",
                columns: new[] { "Tipo", "OrdemServicoId", "VersaoNegocio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecebimentosMensagem_ChaveOperacao",
                schema: "GestaoOrdemServico",
                table: "RecebimentosMensagem",
                column: "ChaveOperacao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxSaga",
                schema: "GestaoOrdemServico");

            migrationBuilder.DropTable(
                name: "RecebimentosMensagem",
                schema: "GestaoOrdemServico");
        }
    }
}
