using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Oregon.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    default_tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    crt_effects = table.Column<bool>(type: "boolean", nullable: false),
                    text_speed = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.id);
                    table.CheckConstraint("ck_app_settings_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_number = table.Column<int>(type: "integer", nullable: false),
                    workflow_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    captain_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ship_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phase = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    hull = table.Column<int>(type: "integer", nullable: false),
                    crew = table.Column<int>(type: "integer", nullable: false),
                    location_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    state_version = table.Column<long>(type: "bigint", nullable: false),
                    last_view_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                    table.CheckConstraint("ck_games_slot_number", "slot_number BETWEEN 1 AND 10");
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_entries_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_games_slot_number",
                table: "games",
                column: "slot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_games_workflow_id",
                table: "games",
                column: "workflow_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_game_id_sequence",
                table: "journal_entries",
                columns: new[] { "game_id", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
