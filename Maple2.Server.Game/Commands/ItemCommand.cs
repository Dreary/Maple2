using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using Maple2.Database.Storage;
using Maple2.Model;
using Maple2.Model.Game;
using Maple2.Server.Game.Model;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.Commands;

public class ItemCommand : Command {
    private const string NAME = "item";
    private const string DESCRIPTION = "Item spawning.";

    private const int MAX_RARITY = 6;
    private const int MAX_SOCKET = 5;

    private readonly GameSession session;
    private readonly ItemMetadataStorage itemStorage;

    public ItemCommand(GameSession session, ItemMetadataStorage itemStorage) : base(NAME, DESCRIPTION) {
        this.session = session;
        this.itemStorage = itemStorage;

        var id = new Argument<int>("id", "Id of item to spawn.");
        var amount = new Option<int>(new[] { "--amount", "-a" }, () => 1, "Amount of the item.");
        var rarity = new Option<int>(new[] { "--rarity", "-r" }, () => 1, "Rarity of the item.");
        var socket = new Option<int>(new[] { "--socket", "-s" }, () => 0, "Number of sockets.");
        var drop = new Option<bool>(new[] { "--drop" }, "Drop item instead of adding to inventory");
        var rollMax = new Option<bool>(new[] { "--roll-max" }, () => false, "Max roll stats.");

        AddArgument(id);
        AddOption(amount);
        AddOption(rarity);
        AddOption(socket);
        AddOption(drop);
        AddOption(rollMax);
        this.SetHandler<InvocationContext, int, int, int, int, bool, bool>(Handle, id, amount, rarity, socket, drop, rollMax);
    }

    private void Handle(InvocationContext ctx, int itemId, int amount, int rarity, int socket, bool drop, bool rollMax) {
        try {
            rarity = Math.Clamp(rarity, 1, MAX_RARITY);
            socket = Math.Clamp(socket, 0, MAX_SOCKET);

            // Create the item with the specified socket count
            Item? item = session.Field.ItemDrop.CreateItem(itemId, rarity, amount, rollMax, socket);
            if (item == null) {
                ctx.Console.Error.WriteLine($"Invalid Item: {itemId}");
                return;
            }

            if (!item.IsCurrency()) {
                if (item.Metadata.Property.SlotMax == 0) {
                    ctx.Console.Error.WriteLine($"{itemId} has SlotMax of 0, ignoring...");
                    amount = Math.Clamp(amount, 1, int.MaxValue);
                } else {
                    amount = Math.Clamp(amount, 1, item.Metadata.Property.SlotMax);
                }
            }
            item.Amount = amount;

            using (GameStorage.Request db = session.GameStorage.Context()) {
                item = db.CreateItem(session.CharacterId, item);
                if (item == null) {
                    ctx.Console.Error.WriteLine($"Failed to create item:{itemId} in database");
                    ctx.ExitCode = 1;
                    return;
                }
            }

            if (drop && session.Field != null) {
                FieldItem fieldItem = session.Field.SpawnItem(session.Player, item);
                session.Field.Broadcast(FieldPacket.DropItem(fieldItem));
            } else if (!session.Item.Inventory.Add(item, true)) {
                session.Item.Inventory.Discard(item);
                ctx.Console.Error.WriteLine($"Failed to add item:{item.Id} to inventory");
                ctx.ExitCode = 1;
                return;
            }

            ctx.ExitCode = 0;
        } catch (SystemException ex) {
            ctx.Console.Error.WriteLine(ex.Message);
            ctx.ExitCode = 1;
        }
    }
}
