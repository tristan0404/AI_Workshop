using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Workshop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceClosesAtUtc",
                table: "LectureSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceOpenedAtUtc",
                table: "LectureSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceState",
                table: "LectureSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FallbackCodeProtected",
                table: "LectureSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LectureSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequiresReview = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_LectureSessions_LectureSessionId",
                        column: x => x.LectureSessionId,
                        principalTable: "LectureSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_LectureSessionId_StudentId",
                table: "AttendanceRecords",
                columns: new[] { "LectureSessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId",
                table: "AttendanceRecords",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "AttendanceClosesAtUtc",
                table: "LectureSessions");

            migrationBuilder.DropColumn(
                name: "AttendanceOpenedAtUtc",
                table: "LectureSessions");

            migrationBuilder.DropColumn(
                name: "AttendanceState",
                table: "LectureSessions");

            migrationBuilder.DropColumn(
                name: "FallbackCodeProtected",
                table: "LectureSessions");
        }
    }
}
