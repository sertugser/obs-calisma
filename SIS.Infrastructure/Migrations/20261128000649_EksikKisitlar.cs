using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EksikKisitlar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentGrade_Score_Aralik",
                table: "StudentGrades",
                sql: "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradeComponent_Weight_Aralik",
                table: "GradeComponents",
                sql: "\"Weight\" >= 0 AND \"Weight\" <= 100");

            // Tek etkin dönem. EF modelinde karşılığı yok (elle yazılmış SQL). Ertelemeli: dönem
            // değiştirirken eskiyi kapatıp yeniyi açan tek kayıt, satır sırasından bağımsız olarak
            // işlem sonunda denetlenir.
            migrationBuilder.Sql("""
                ALTER TABLE "AcademicTerms" ADD CONSTRAINT "EX_AcademicTerms_TekEtkinDonem"
                    EXCLUDE USING btree ((1) WITH =) WHERE ("IsActivated" AND NOT "IsCancelled")
                    DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "AcademicTerms" DROP CONSTRAINT "EX_AcademicTerms_TekEtkinDonem";""");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudentGrade_Score_Aralik",
                table: "StudentGrades");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeComponent_Weight_Aralik",
                table: "GradeComponents");
        }
    }
}
