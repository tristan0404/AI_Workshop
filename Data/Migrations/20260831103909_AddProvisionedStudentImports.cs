using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Workshop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProvisionedStudentImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceImportItems_BatchId_StudentId_LectureDate",
                table: "AttendanceImportItems");

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "AttendanceImportItems",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "StudentAction",
                table: "AttendanceImportItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsProvisioned",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportItems_BatchId_StudentNumber_LectureDate",
                table: "AttendanceImportItems",
                columns: new[] { "BatchId", "StudentNumber", "LectureDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceImportItems_BatchId_StudentNumber_LectureDate",
                table: "AttendanceImportItems");

            migrationBuilder.DropColumn(
                name: "StudentAction",
                table: "AttendanceImportItems");

            migrationBuilder.DropColumn(
                name: "IsProvisioned",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "AttendanceImportItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportItems_BatchId_StudentId_LectureDate",
                table: "AttendanceImportItems",
                columns: new[] { "BatchId", "StudentId", "LectureDate" },
                unique: true);
        }
    }
}
