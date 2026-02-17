using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maple2.Server.World.Migrations
{
    /// <inheritdoc />
    public partial class AddDungeonRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Must drop FK before dropping PK (MySQL constraint)
            migrationBuilder.DropForeignKey(
                name: "FK_raid-record_character_OwnerId",
                table: "raid-record");

            migrationBuilder.DropPrimaryKey(
                name: "PK_raid-record",
                table: "raid-record");

            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "raid-record",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_raid-record",
                table: "raid-record",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_raid-record_OwnerId_DungeonRoomId",
                table: "raid-record",
                columns: new[] { "OwnerId", "DungeonRoomId" },
                unique: true);

            // Re-add FK on OwnerId
            migrationBuilder.AddForeignKey(
                name: "FK_raid-record_character_OwnerId",
                table: "raid-record",
                column: "OwnerId",
                principalTable: "character",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_raid-record",
                table: "raid-record");

            migrationBuilder.DropIndex(
                name: "IX_raid-record_OwnerId_DungeonRoomId",
                table: "raid-record");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "raid-record");

            migrationBuilder.AddPrimaryKey(
                name: "PK_raid-record",
                table: "raid-record",
                columns: new[] { "OwnerId", "DungeonRoomId" });
        }
    }
}
