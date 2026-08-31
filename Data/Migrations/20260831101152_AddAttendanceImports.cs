using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Workshop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CourseId = table.Column<int>(type: "INTEGER", nullable: false),
                    LecturerId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SpreadsheetRowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceImportBatches_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceImportErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpreadsheetRow = table.Column<int>(type: "INTEGER", nullable: true),
                    Column = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceImportErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceImportErrors_AttendanceImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AttendanceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceImportItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpreadsheetRow = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    StudentNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StudentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LectureDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LectureSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceImportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceImportItems_AttendanceImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AttendanceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportBatches_CourseId",
                table: "AttendanceImportBatches",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportBatches_LecturerId_UploadedAtUtc",
                table: "AttendanceImportBatches",
                columns: new[] { "LecturerId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportErrors_BatchId",
                table: "AttendanceImportErrors",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceImportItems_BatchId_StudentId_LectureDate",
                table: "AttendanceImportItems",
                columns: new[] { "BatchId", "StudentId", "LectureDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceImportErrors");

            migrationBuilder.DropTable(
                name: "AttendanceImportItems");

            migrationBuilder.DropTable(
                name: "AttendanceImportBatches");
        }
    }
}
