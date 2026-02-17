using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class MergeRaidRecordAndPartyClear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raid-party-clear");

            migrationBuilder.AddColumn<int>(
                name: "BestClearSeconds",
                table: "raid-record",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BestClearTimestamp",
                table: "raid-record",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FirstClearSeconds",
                table: "raid-record",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstClearTimestamp",
                table: "raid-record",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PartyMemberIds",
                table: "raid-record",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestClearSeconds",
                table: "raid-record");

            migrationBuilder.DropColumn(
                name: "BestClearTimestamp",
                table: "raid-record");

            migrationBuilder.DropColumn(
                name: "FirstClearSeconds",
                table: "raid-record");

            migrationBuilder.DropColumn(
                name: "FirstClearTimestamp",
                table: "raid-record");

            migrationBuilder.DropColumn(
                name: "PartyMemberIds",
                table: "raid-record");

            migrationBuilder.CreateTable(
                name: "raid-party-clear",
                columns: table => new
                {
                    LeaderCharacterId = table.Column<long>(type: "bigint", nullable: false),
                    DungeonRoomId = table.Column<int>(type: "int", nullable: false),
                    BestClearSeconds = table.Column<int>(type: "int", nullable: false),
                    BestClearTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FirstClearSeconds = table.Column<int>(type: "int", nullable: false),
                    FirstClearTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PartyMemberIds = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raid-party-clear", x => new { x.LeaderCharacterId, x.DungeonRoomId });
                    table.ForeignKey(
                        name: "FK_raid-party-clear_character_LeaderCharacterId",
                        column: x => x.LeaderCharacterId,
                        principalTable: "character",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
