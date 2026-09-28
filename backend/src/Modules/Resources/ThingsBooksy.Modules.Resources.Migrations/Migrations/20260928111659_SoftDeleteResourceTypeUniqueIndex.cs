using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThingsBooksy.Modules.Resources.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteResourceTypeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_types_GroupId_Name",
                schema: "resources",
                table: "resource_types");

            migrationBuilder.CreateIndex(
                name: "IX_resource_types_GroupId_Name",
                schema: "resources",
                table: "resource_types",
                columns: new[] { "GroupId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_resource_types_GroupId_Name",
                schema: "resources",
                table: "resource_types");

            migrationBuilder.CreateIndex(
                name: "IX_resource_types_GroupId_Name",
                schema: "resources",
                table: "resource_types",
                columns: new[] { "GroupId", "Name" },
                unique: true);
        }
    }
}
