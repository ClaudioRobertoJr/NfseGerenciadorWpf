using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NfseGerenciador.Migrations
{
    /// <inheritdoc />
    public partial class NacionalAdn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Cancelada",
                table: "Nfses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChaveAcessoNacional",
                table: "Nfses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NacionalEvento",
                columns: table => new
                {
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    Nsu = table.Column<long>(type: "bigint", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cancela = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NacionalEvento", x => new { x.Cnpj, x.Nsu });
                });

            migrationBuilder.CreateTable(
                name: "NacionalSync",
                columns: table => new
                {
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    UltimoNsu = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NacionalSync", x => x.Cnpj);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Nfses_ChaveAcessoNacional",
                table: "Nfses",
                column: "ChaveAcessoNacional",
                unique: true,
                filter: "[ChaveAcessoNacional] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NacionalEvento");

            migrationBuilder.DropTable(
                name: "NacionalSync");

            migrationBuilder.DropIndex(
                name: "IX_Nfses_ChaveAcessoNacional",
                table: "Nfses");

            migrationBuilder.DropColumn(
                name: "Cancelada",
                table: "Nfses");

            migrationBuilder.DropColumn(
                name: "ChaveAcessoNacional",
                table: "Nfses");
        }
    }
}
