using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NfseGerenciador.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoImportacaoBaixas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotesBaixas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HashArquivo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    PrestadorCnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    ImportadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesBaixas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosBaixas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteBaixaId = table.Column<int>(type: "int", nullable: false),
                    NfseId = table.Column<int>(type: "int", nullable: false),
                    Impressao = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Planilha = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Linha = table.Column<int>(type: "int", nullable: false),
                    Historico = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Centro = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contrapartida = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataRelatorio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstadoPosterior = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosBaixas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosBaixas_LotesBaixas_LoteBaixaId",
                        column: x => x.LoteBaixaId,
                        principalTable: "LotesBaixas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosBaixas_Nfses_NfseId",
                        column: x => x.NfseId,
                        principalTable: "Nfses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosBaixas_Impressao",
                table: "RegistrosBaixas",
                column: "Impressao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosBaixas_LoteBaixaId",
                table: "RegistrosBaixas",
                column: "LoteBaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosBaixas_NfseId",
                table: "RegistrosBaixas",
                column: "NfseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosBaixas");

            migrationBuilder.DropTable(
                name: "LotesBaixas");
        }
    }
}
