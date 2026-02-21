using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureShare.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EncryptionKey",
                table: "FileRecords",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "IV",
                table: "FileRecords",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionKey",
                table: "FileRecords");

            migrationBuilder.DropColumn(
                name: "IV",
                table: "FileRecords");
        }
    }
}
