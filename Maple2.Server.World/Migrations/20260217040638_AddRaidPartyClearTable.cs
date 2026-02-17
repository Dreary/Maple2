using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidPartyClearTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raid-party-clear",
                columns: table => new
                {
                    LeaderCharacterId = table.Column<long>(type: "bigint", nullable: false),
                    DungeonRoomId = table.Column<int>(type: "int", nullable: false),
                    FirstClearTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FirstClearSeconds = table.Column<int>(type: "int", nullable: false),
                    BestClearSeconds = table.Column<int>(type: "int", nullable: false),
                    BestClearTimestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raid-party-clear");
        }
    }
}
