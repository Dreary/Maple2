using Maple2.Database.Extensions;
using Maple2.Database.Model.Ranking;

namespace Maple2.Database.Storage;

public partial class GameStorage {
    public partial class Request {
        public bool SaveRaidRecords(long ownerId, Dictionary<int, int> pendingClears,
            (int DungeonRoomId, int ClearSeconds, long[] PartyMemberIds)? partyClear = null) {
            if (pendingClears.Count == 0 && partyClear == null) {
                return true;
            }

            DateTime now = DateTime.Now;
            var tracked = new Dictionary<int, Model.RaidRecord>();

            foreach ((int dungeonRoomId, int clearsToAdd) in pendingClears) {
                Model.RaidRecord? existing = Context.RaidRecord
                    .FirstOrDefault(r => r.OwnerId == ownerId && r.DungeonRoomId == dungeonRoomId);

                if (existing != null) {
                    existing.TotalClears += clearsToAdd;
                    existing.LastClearTime = now;
                    tracked[dungeonRoomId] = existing;
                } else {
                    var record = new Model.RaidRecord {
                        OwnerId = ownerId,
                        DungeonRoomId = dungeonRoomId,
                        TotalClears = clearsToAdd,
                        LastClearTime = now,
                    };
                    Context.RaidRecord.Add(record);
                    tracked[dungeonRoomId] = record;
                }
            }

            if (partyClear.HasValue) {
                var (dungeonRoomId, clearSeconds, partyMemberIds) = partyClear.Value;
                string memberIds = string.Join(",", partyMemberIds);

                // Reuse entity from pendingClears if same dungeonRoomId, otherwise query DB
                if (!tracked.TryGetValue(dungeonRoomId, out Model.RaidRecord? record)) {
                    record = Context.RaidRecord
                        .FirstOrDefault(r => r.OwnerId == ownerId && r.DungeonRoomId == dungeonRoomId);
                }

                if (record != null) {
                    if (record.FirstClearTimestamp == default) {
                        record.FirstClearTimestamp = now;
                        record.FirstClearSeconds = clearSeconds;
                    }

                    if (record.BestClearSeconds == 0 || clearSeconds < record.BestClearSeconds) {
                        record.BestClearSeconds = clearSeconds;
                        record.BestClearTimestamp = now;
                    }

                    record.PartyMemberIds = memberIds;
                } else {
                    Context.RaidRecord.Add(new Model.RaidRecord {
                        OwnerId = ownerId,
                        DungeonRoomId = dungeonRoomId,
                        FirstClearTimestamp = now,
                        FirstClearSeconds = clearSeconds,
                        BestClearSeconds = clearSeconds,
                        BestClearTimestamp = now,
                        PartyMemberIds = memberIds,
                        LastClearTime = now,
                    });
                }
            }

            return Context.TrySaveChanges();
        }

        public IList<RaidEarlyVictoryRankInfo> GetRaidEarlyVictoryRankings(int bossRankingId, int[] dungeonRoomIds) {
            if (dungeonRoomIds.Length == 0) {
                return [];
            }

            var records = Context.RaidRecord
                .Where(r => dungeonRoomIds.Contains(r.DungeonRoomId) && r.FirstClearSeconds > 0)
                .OrderBy(r => r.FirstClearTimestamp)
                .Take(200)
                .ToList();

            if (records.Count == 0) {
                return [];
            }

            // Collect all character IDs (leaders + members)
            var allCharacterIds = new HashSet<long>();
            foreach (var record in records) {
                allCharacterIds.Add(record.OwnerId);
                foreach (string id in record.PartyMemberIds.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                    if (long.TryParse(id, out long memberId)) {
                        allCharacterIds.Add(memberId);
                    }
                }
            }

            var characters = Context.Character
                .Where(c => allCharacterIds.Contains(c.Id))
                .Select(c => new {
                    c.Id,
                    c.AccountId,
                    c.Name,
                    Profile = c.Profile.Picture,
                    c.Level,
                    c.Job,
                })
                .ToDictionary(c => c.Id);

            return records
                .Select((r, index) => {
                    var leaderName = characters.TryGetValue(r.OwnerId, out var leader) ? leader.Name : string.Empty;

                    var members = r.PartyMemberIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => long.TryParse(id, out long memberId) && characters.TryGetValue(memberId, out var c)
                            ? new RaidPartyMemberInfo(c.AccountId, c.Name, c.Profile ?? string.Empty, c.Level, (int) c.Job)
                            : null)
                        .Where(m => m != null)
                        .Cast<RaidPartyMemberInfo>()
                        .ToList();

                    return new RaidEarlyVictoryRankInfo(
                        Rank: index + 1,
                        DungeonRecordId: r.Id,
                        LeaderCharacterId: r.OwnerId,
                        LeaderName: leaderName,
                        BossRankingId: bossRankingId,
                        ClearSeconds: r.FirstClearSeconds,
                        ClearTimestamp: r.FirstClearTimestamp,
                        PartyMembers: members);
                })
                .ToList();
        }

        public IList<RaidShortestTimeRankInfo> GetRaidShortestTimeRankings(int bossRankingId, int[] dungeonRoomIds) {
            if (dungeonRoomIds.Length == 0) {
                return [];
            }

            var records = Context.RaidRecord
                .Where(r => dungeonRoomIds.Contains(r.DungeonRoomId) && r.BestClearSeconds > 0)
                .OrderBy(r => r.BestClearSeconds)
                .Take(200)
                .ToList();

            if (records.Count == 0) {
                return [];
            }

            var characterIds = records.Select(r => r.OwnerId).Distinct().ToList();
            var characters = Context.Character
                .Where(c => characterIds.Contains(c.Id))
                .Select(c => new {
                    c.Id,
                    c.Name,
                    Profile = c.Profile.Picture,
                })
                .ToDictionary(c => c.Id);

            return records
                .Select((r, index) => {
                    characters.TryGetValue(r.OwnerId, out var c);
                    return new RaidShortestTimeRankInfo(
                        Rank: index + 1,
                        LeaderCharacterId: r.OwnerId,
                        LeaderName: c?.Name ?? string.Empty,
                        LeaderProfile: c?.Profile ?? string.Empty,
                        BossRankingId: bossRankingId,
                        ClearSeconds: r.BestClearSeconds,
                        ClearTimestamp: r.BestClearTimestamp);
                })
                .ToList();
        }
    }
}
