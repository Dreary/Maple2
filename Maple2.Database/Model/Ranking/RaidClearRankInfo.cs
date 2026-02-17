namespace Maple2.Database.Model.Ranking;

public record RaidClearRankInfo(
    int Rank,
    long CharacterId,
    string Name,
    string Profile,
    int BossRankingId,
    int TotalClears,
    int JobCode,
    int Level);
