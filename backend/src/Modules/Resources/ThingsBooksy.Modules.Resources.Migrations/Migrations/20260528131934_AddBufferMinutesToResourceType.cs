using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThingsBooksy.Modules.Resources.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddBufferMinutesToResourceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                schema: "resources",
                table: "resource_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                schema: "resources",
                table: "resource_types");
        }
    }
}
