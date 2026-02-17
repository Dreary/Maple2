using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maple2.Database.Model;

internal class RaidRecord {
    public long Id { get; set; }
    public long OwnerId { get; set; }
    public int DungeonRoomId { get; set; }
    public int TotalClears { get; set; }
    public DateTime LastClearTime { get; set; }
    public int FirstClearSeconds { get; set; }
    public DateTime FirstClearTimestamp { get; set; }
    public int BestClearSeconds { get; set; }
    public DateTime BestClearTimestamp { get; set; }
    public string PartyMemberIds { get; set; } = string.Empty;

    public static void Configure(EntityTypeBuilder<RaidRecord> builder) {
        builder.ToTable("raid-record");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();
        builder.HasIndex(r => new { r.OwnerId, r.DungeonRoomId }).IsUnique();
        builder.HasOne<Character>()
            .WithMany()
            .HasForeignKey(r => r.OwnerId);
    }
}
