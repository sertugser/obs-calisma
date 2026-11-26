using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKatalogTablolari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KatalogAdimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Calisti = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KatalogAdimlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KatalogKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Anahtar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IlkYazilma = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KatalogKayitlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KatalogAdimlari_Ad",
                table: "KatalogAdimlari",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KatalogKayitlari_Tur_Anahtar",
                table: "KatalogKayitlari",
                columns: new[] { "Tur", "Anahtar" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KatalogAdimlari");

            migrationBuilder.DropTable(
                name: "KatalogKayitlari");
        }
    }
}
