using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThingsBooksy.Modules.Availability.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialAvailabilitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "availability");

            migrationBuilder.CreateTable(
                name: "group_read_models",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_read_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "public_holiday_read_models",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LocalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_holiday_read_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resource_read_models",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_read_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resource_rule_sets",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaId = table.Column<Guid>(type: "uuid", nullable: false),
                    BufferMinutesOverride = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_rule_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "schema_read_models",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultBufferMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_read_models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "schema_rule_sets",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BufferMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_rule_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "availability_rules",
                schema: "availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaRuleSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceRuleSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    RuleMode = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeek = table.Column<int[]>(type: "integer[]", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndDayOffset = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_availability_rules_resource_rule_sets_ResourceRuleSetId",
                        column: x => x.ResourceRuleSetId,
                        principalSchema: "availability",
                        principalTable: "resource_rule_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_availability_rules_schema_rule_sets_SchemaRuleSetId",
                        column: x => x.SchemaRuleSetId,
                        principalSchema: "availability",
                        principalTable: "schema_rule_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_availability_rules_ResourceRuleSetId",
                schema: "availability",
                table: "availability_rules",
                column: "ResourceRuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_availability_rules_SchemaRuleSetId",
                schema: "availability",
                table: "availability_rules",
                column: "SchemaRuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_public_holiday_read_models_Year_Date",
                schema: "availability",
                table: "public_holiday_read_models",
                columns: new[] { "Year", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_rule_sets_ResourceId",
                schema: "availability",
                table: "resource_rule_sets",
                column: "ResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schema_rule_sets_SchemaId",
                schema: "availability",
                table: "schema_rule_sets",
                column: "SchemaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_rules",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "group_read_models",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "public_holiday_read_models",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "resource_read_models",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "schema_read_models",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "resource_rule_sets",
                schema: "availability");

            migrationBuilder.DropTable(
                name: "schema_rule_sets",
                schema: "availability");
        }
    }
}
