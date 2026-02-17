using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidRecordTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raid-record",
                columns: table => new
                {
                    OwnerId = table.Column<long>(type: "bigint", nullable: false),
                    DungeonRoomId = table.Column<int>(type: "int", nullable: false),
                    TotalClears = table.Column<int>(type: "int", nullable: false),
                    LastClearTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raid-record", x => new { x.OwnerId, x.DungeonRoomId });
                    table.ForeignKey(
                        name: "FK_raid-record_character_OwnerId",
                        column: x => x.OwnerId,
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
                name: "raid-record");
        }
    }
}
