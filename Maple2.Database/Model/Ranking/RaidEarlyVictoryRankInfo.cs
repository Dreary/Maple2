namespace Maple2.Database.Model.Ranking;

public record RaidEarlyVictoryRankInfo(
    int Rank,
    long DungeonRecordId,
    long LeaderCharacterId,
    string LeaderName,
    int BossRankingId,
    int ClearSeconds,
    DateTime ClearTimestamp,
    IList<RaidPartyMemberInfo> PartyMembers);

public record RaidPartyMemberInfo(
    long AccountId,
    string Name,
    string Profile,
    int Level,
    int Job);
