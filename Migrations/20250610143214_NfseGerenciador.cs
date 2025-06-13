using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NfseGerenciador.Migrations
{
    /// <inheritdoc />
    public partial class NfseGerenciador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmpresasClassificadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cnpj = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoClassificacao = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmpresasClassificadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoRecebimento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EhAVista = table.Column<bool>(type: "bit", nullable: false),
                    NumeroParcelas = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nfses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoVerificacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValorLiquidoNfse = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrestadorCnpj = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrestadorRazaoSocial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TomadorCpfCnpj = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TomadorRazaoSocial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstaPago = table.Column<bool>(type: "bit", nullable: false),
                    Local = table.Column<int>(type: "int", nullable: false),
                    PagamentoId = table.Column<int>(type: "int", nullable: true),
                    ClassificacaoEspecial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataPrevistaRecebimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataPagamentoEfetivo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Excluida = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nfses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nfses_Pagamentos_PagamentoId",
                        column: x => x.PagamentoId,
                        principalTable: "Pagamentos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NfseParcelas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NfseId = table.Column<int>(type: "int", nullable: false),
                    NumeroParcela = table.Column<int>(type: "int", nullable: false),
                    ValorParcela = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataVencimentoParcela = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParcelaPaga = table.Column<bool>(type: "bit", nullable: false),
                    DataPagamentoParcela = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfseParcelas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NfseParcelas_Nfses_NfseId",
                        column: x => x.NfseId,
                        principalTable: "Nfses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Pagamentos",
                columns: new[] { "Id", "EhAVista", "NumeroParcelas", "TipoRecebimento" },
                values: new object[,]
                {
                    { 1, true, 1, "Dinheiro" },
                    { 2, true, 1, "Pix" },
                    { 3, false, null, "Cartão de Crédito" },
                    { 4, true, 1, "Cartão de Débito" },
                    { 5, false, null, "Cheque" },
                    { 6, true, 1, "Plano de Saúde" },
                    { 7, true, 1, "Convênio" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmpresasClassificadas_Cnpj",
                table: "EmpresasClassificadas",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NfseParcelas_NfseId",
                table: "NfseParcelas",
                column: "NfseId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfses_PagamentoId",
                table: "Nfses",
                column: "PagamentoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmpresasClassificadas");

            migrationBuilder.DropTable(
                name: "NfseParcelas");

            migrationBuilder.DropTable(
                name: "Nfses");

            migrationBuilder.DropTable(
                name: "Pagamentos");
        }
    }
}
