using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThingsBooksy.Modules.Resources.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameResourceTypeToResourceSchema : Migration
    {
        // Story 016 (DEC-1, research R2): rename-only. The scaffolder emitted DropTable/CreateTable for
        // resource_types -> resource_schemas because the CLR type and the table were renamed together;
        // corrected by hand to RenameTable so existing rows survive (AC-9). PK and FK are re-created
        // under their new names (constraint only, never a table or column).
        // The FK carries the explicit name of DEC-6 (the EF default would exceed PostgreSQL's 63-character limit).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_property_definitions_resource_types_ResourceTypeId",
                schema: "resources",
                table: "resource_property_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_resource_types",
                schema: "resources",
                table: "resource_types");

            migrationBuilder.RenameTable(
                name: "resource_types",
                schema: "resources",
                newName: "resource_schemas",
                newSchema: "resources");

            migrationBuilder.RenameIndex(
                name: "IX_resource_types_GroupId_Name",
                schema: "resources",
                table: "resource_schemas",
                newName: "IX_resource_schemas_GroupId_Name");

            migrationBuilder.RenameColumn(
                name: "ResourceTypeId",
                schema: "resources",
                table: "resource_property_definitions",
                newName: "ResourceSchemaId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_property_definitions_ResourceTypeId",
                schema: "resources",
                table: "resource_property_definitions",
                newName: "IX_resource_property_definitions_ResourceSchemaId");

            migrationBuilder.RenameColumn(
                name: "ResourceTypeId",
                schema: "resources",
                table: "resource_instances",
                newName: "ResourceSchemaId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_resource_schemas",
                schema: "resources",
                table: "resource_schemas",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_property_definitions_resource_schemas",
                schema: "resources",
                table: "resource_property_definitions",
                column: "ResourceSchemaId",
                principalSchema: "resources",
                principalTable: "resource_schemas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_resource_property_definitions_resource_schemas",
                schema: "resources",
                table: "resource_property_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_resource_schemas",
                schema: "resources",
                table: "resource_schemas");

            migrationBuilder.RenameColumn(
                name: "ResourceSchemaId",
                schema: "resources",
                table: "resource_instances",
                newName: "ResourceTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_property_definitions_ResourceSchemaId",
                schema: "resources",
                table: "resource_property_definitions",
                newName: "IX_resource_property_definitions_ResourceTypeId");

            migrationBuilder.RenameColumn(
                name: "ResourceSchemaId",
                schema: "resources",
                table: "resource_property_definitions",
                newName: "ResourceTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_resource_schemas_GroupId_Name",
                schema: "resources",
                table: "resource_schemas",
                newName: "IX_resource_types_GroupId_Name");

            migrationBuilder.RenameTable(
                name: "resource_schemas",
                schema: "resources",
                newName: "resource_types",
                newSchema: "resources");

            migrationBuilder.AddPrimaryKey(
                name: "PK_resource_types",
                schema: "resources",
                table: "resource_types",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_resource_property_definitions_resource_types_ResourceTypeId",
                schema: "resources",
                table: "resource_property_definitions",
                column: "ResourceTypeId",
                principalSchema: "resources",
                principalTable: "resource_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
