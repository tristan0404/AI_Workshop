using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_Workshop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceQueriesAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceQueries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LectureSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LecturerResponse = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedByLecturerId = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    EvidenceContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EvidenceContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceQueries_AspNetUsers_ReviewedByLecturerId",
                        column: x => x.ReviewedByLecturerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceQueries_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceQueries_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceQueries_LectureSessions_LectureSessionId",
                        column: x => x.LectureSessionId,
                        principalTable: "LectureSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LectureSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttendanceQueryId = table.Column<int>(type: "INTEGER", nullable: true),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedByLecturerId = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeLogs_AspNetUsers_ChangedByLecturerId",
                        column: x => x.ChangedByLecturerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeLogs_AttendanceQueries_AttendanceQueryId",
                        column: x => x.AttendanceQueryId,
                        principalTable: "AttendanceQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeLogs_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeLogs_LectureSessions_LectureSessionId",
                        column: x => x.LectureSessionId,
                        principalTable: "LectureSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeLogs_AttendanceQueryId",
                table: "AttendanceChangeLogs",
                column: "AttendanceQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeLogs_AttendanceRecordId_ChangedAtUtc",
                table: "AttendanceChangeLogs",
                columns: new[] { "AttendanceRecordId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeLogs_ChangedByLecturerId",
                table: "AttendanceChangeLogs",
                column: "ChangedByLecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeLogs_LectureSessionId",
                table: "AttendanceChangeLogs",
                column: "LectureSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceQueries_AttendanceRecordId",
                table: "AttendanceQueries",
                column: "AttendanceRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceQueries_LectureSessionId_StudentId_Status",
                table: "AttendanceQueries",
                columns: new[] { "LectureSessionId", "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceQueries_ReviewedByLecturerId",
                table: "AttendanceQueries",
                column: "ReviewedByLecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceQueries_StudentId",
                table: "AttendanceQueries",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceChangeLogs");

            migrationBuilder.DropTable(
                name: "AttendanceQueries");
        }
    }
}
