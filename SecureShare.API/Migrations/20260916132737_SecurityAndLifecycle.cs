using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureShare.API.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EncryptionVersion",
                table: "FileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "KeyProtected",
                table: "FileRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "FileRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "FileRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "FileRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                table: "AccessLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_IsActive_ExpiresAt",
                table: "FileRecords",
                columns: new[] { "IsActive", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_OwnerId",
                table: "FileRecords",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLogs_OperationId",
                table: "AccessLogs",
                column: "OperationId",
                unique: true,
                filter: "[OperationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileRecords_IsActive_ExpiresAt",
                table: "FileRecords");

            migrationBuilder.DropIndex(
                name: "IX_FileRecords_OwnerId",
                table: "FileRecords");

            migrationBuilder.DropIndex(
                name: "IX_AccessLogs_OperationId",
                table: "AccessLogs");

            migrationBuilder.DropColumn(
                name: "EncryptionVersion",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "KeyProtected",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "AccessLogs");
        }
    }
}
