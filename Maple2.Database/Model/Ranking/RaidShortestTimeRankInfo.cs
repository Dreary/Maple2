namespace Maple2.Database.Model.Ranking;

public record RaidShortestTimeRankInfo(
    int Rank,
    long LeaderCharacterId,
    string LeaderName,
    string LeaderProfile,
    int BossRankingId,
    int ClearSeconds,
    DateTime ClearTimestamp);
