﻿using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.Server.Game.Model;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.Manager.Field;

public partial class FieldManager {
    private int objectIdCounter = 10000000;

    private readonly ConcurrentDictionary<int, FieldPlayer> fieldPlayers =
        new ConcurrentDictionary<int, FieldPlayer>();

    public FieldPlayer SpawnPlayer(GameSession session, Player player) {
        // TODO: Not sure what the difference is between instance ids.
        player.Character.MapId = MapId;
        player.Character.InstanceMapId = MapId;
        player.Character.InstanceId = InstanceId;

        int objectId = Interlocked.Increment(ref objectIdCounter);
        var fieldPlayer = new FieldPlayer(objectId, session, player);

        SpawnPointPC? spawn = entities.PlayerSpawns.Values.FirstOrDefault(spawn => spawn.Enable);
        if (spawn != null) {
            fieldPlayer.Position = spawn.Position;
            fieldPlayer.Rotation = spawn.Rotation;
        }

        return fieldPlayer;
    }

    public bool RemovePlayer(int objectId, out FieldPlayer? fieldPlayer) {
        return fieldPlayers.TryRemove(objectId, out fieldPlayer);
    }

    #region Events
    public void OnAddPlayer(FieldPlayer added) {
        fieldPlayers[added.ObjectId] = added;
        // LOAD:
        // Liftable
        // Breakable
        // InteractObject
        foreach (FieldPlayer fieldPlayer in fieldPlayers.Values) {
            added.Session.Send(FieldPacket.AddPlayer(fieldPlayer.Session));
        }
        Multicast(FieldPacket.AddPlayer(added.Session), added.Session); // FieldAddUser
        // FieldAddNpc
        // FieldAddPet
        // FieldAddPortal
        // ProxyGameObj
        // RegionSkill (on tick?)
        // Stat
    }
    #endregion Events
}