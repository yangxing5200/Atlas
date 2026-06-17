using System;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Data.Global.Migrations.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AtlasGlobalDbContext))]
    [Migration("20260615093000_v0.2.4-background-job-cancellation")]
    public partial class v024backgroundjobcancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAt",
                table: "BackgroundJobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRequestedBy",
                table: "BackgroundJobs",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "BackgroundJobs",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_CancellationRequested",
                table: "BackgroundJobs",
                columns: new[] { "Status", "CancellationRequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_CancellationRequested",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedBy",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "BackgroundJobs");
        }
    }
}
