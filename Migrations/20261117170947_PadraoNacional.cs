using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NfseGerenciador.Migrations
{
    /// <inheritdoc />
    public partial class PadraoNacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvaliacoesNacionais",
                columns: table => new
                {
                    NfseId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VersaoRegra = table.Column<int>(type: "int", nullable: false),
                    VersaoLeiaute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvaliadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resumo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CamposJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesNacionais", x => x.NfseId);
                    table.ForeignKey(
                        name: "FK_AvaliacoesNacionais_Nfses_NfseId",
                        column: x => x.NfseId,
                        principalTable: "Nfses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosNacionais",
                columns: table => new
                {
                    NfseId = table.Column<int>(type: "int", nullable: false),
                    ArquivoXmlGzipBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosNacionais", x => x.NfseId);
                    table.ForeignKey(
                        name: "FK_DocumentosNacionais_AvaliacoesNacionais_NfseId",
                        column: x => x.NfseId,
                        principalTable: "AvaliacoesNacionais",
                        principalColumn: "NfseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesNacionais_Status",
                table: "AvaliacoesNacionais",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosNacionais");

            migrationBuilder.DropTable(
                name: "AvaliacoesNacionais");
        }
    }
}
