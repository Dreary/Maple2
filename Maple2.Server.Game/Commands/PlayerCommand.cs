using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using Maple2.Model;
using Maple2.Model.Enum;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.Database.Storage;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;
using Maple2.Server.Game.Manager.Items;
using Maple2.Server.Core.Network;

namespace Maple2.Server.Game.Commands;

public class PlayerCommand : Command {
    private const string NAME = "player";
    private const string DESCRIPTION = "Player management.";

    public PlayerCommand(GameSession session) : base(NAME, DESCRIPTION) {
        AddCommand(new LevelCommand(session));
        AddCommand(new PrestigeCommand(session));
        AddCommand(new ExpCommand(session));
        AddCommand(new JobCommand(session));
        AddCommand(new InfoCommand(session));
        AddCommand(new SkillPointCommand(session));
        AddCommand(new CurrencyCommand(session));
        AddCommand(new DailyResetCommand(session));
        AddCommand(new InventoryCommand(session));
        AddCommand(new SetGearCommand(session)); // Adding the SetGear command here
    }

    private class LevelCommand : Command {
        private readonly GameSession session;

        public LevelCommand(GameSession session) : base("level", "Set player level.") {
            this.session = session;

            var level = new Argument<short>("level", "Level of the player.");

            AddArgument(level);
            this.SetHandler<InvocationContext, short>(Handle, level);
        }

        private void Handle(InvocationContext ctx, short level) {
            try {
                if (level is < 1 or > Constant.characterMaxLevel) {
                    ctx.Console.Error.WriteLine($"Invalid level: {level}. Must be between 1 and {Constant.characterMaxLevel}.");
                    return;
                }

                session.Player.Value.Character.Level = level;
                session.Field?.Broadcast(LevelUpPacket.LevelUp(session.Player));
                session.Stats.Refresh();

                session.ConditionUpdate(ConditionType.level, targetLong: level);

                session.PlayerInfo.SendUpdate(new PlayerUpdateRequest {
                    AccountId = session.AccountId,
                    CharacterId = session.CharacterId,
                    Level = level,
                    Async = true,
                });

                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class ExpCommand : Command {
        private readonly GameSession session;

        public ExpCommand(GameSession session) : base("exp", "Add player experience.") {
            this.session = session;

            var exp = new Argument<long>("exp", "Exp amount.");

            AddArgument(exp);
            this.SetHandler<InvocationContext, long>(Handle, exp);
        }

        private void Handle(InvocationContext ctx, long exp) {
            try {
                session.Exp.AddExp(ExpType.none, exp);

                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class PrestigeCommand : Command {
        private readonly GameSession session;

        public PrestigeCommand(GameSession session) : base("prestige", "Sets prestige level") {
            this.session = session;

            var level = new Argument<int>("level", "Prestige level of the player.");
            AddArgument(level);
            this.SetHandler<InvocationContext, int>(Handle, level);
        }

        private void Handle(InvocationContext ctx, int level) {
            try {
                if (level is < 1 or > Constant.AdventureLevelLimit) {
                    ctx.Console.Error.WriteLine($"Invalid level: {level}. Must be between 1 and {Constant.AdventureLevelLimit}.");
                    return;
                }

                int currentLevel = session.Exp.PrestigeLevel;
                session.Exp.PrestigeLevelUp(level - currentLevel);

                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class JobCommand : Command {
        private readonly GameSession session;

        public JobCommand(GameSession session) : base("job", "Set player job.") {
            this.session = session;

            var jobCode = new Argument<JobCode>("jobcode", "JobCode of the player.");
            var awakening = new Option<bool>("awakening", "Awakening job advancement.");

            AddArgument(jobCode);
            AddOption(awakening);
            this.SetHandler<InvocationContext, JobCode, bool>(Handle, jobCode, awakening);
        }

        private void Handle(InvocationContext ctx, JobCode jobCode, bool awakening) {
            try {
                Job job = jobCode switch {
                    JobCode.Newbie => Job.Newbie,
                    JobCode.Knight => awakening ? Job.KnightII : Job.Knight,
                    JobCode.Berserker => awakening ? Job.BerserkerII : Job.Berserker,
                    JobCode.Wizard => awakening ? Job.WizardII : Job.Wizard,
                    JobCode.Priest => awakening ? Job.PriestII : Job.Priest,
                    JobCode.Archer => awakening ? Job.ArcherII : Job.Archer,
                    JobCode.HeavyGunner => awakening ? Job.HeavyGunnerII : Job.HeavyGunner,
                    JobCode.Thief => awakening ? Job.ThiefII : Job.Thief,
                    JobCode.Assassin => awakening ? Job.AssassinII : Job.Assassin,
                    JobCode.RuneBlader => awakening ? Job.RuneBladerII : Job.RuneBlader,
                    JobCode.Striker => awakening ? Job.StrikerII : Job.Striker,
                    JobCode.SoulBinder => awakening ? Job.SoulBinderII : Job.SoulBinder,
                    _ => throw new ArgumentException($"Invalid JobCode: {jobCode}")
                };

                Job currentJob = session.Player.Value.Character.Job;
                if (currentJob.Code() != job.Code()) {
                    foreach (SkillTab skillTab in session.Config.Skill.SkillBook.SkillTabs) {
                        skillTab.Skills.Clear();
                    }
                } else if (job < currentJob) {
                    foreach (SkillTab skillTab in session.Config.Skill.SkillBook.SkillTabs) {
                        foreach (int skillId in skillTab.Skills.Keys.ToList()) {
                            if (session.Config.Skill.SkillInfo.GetMainSkill(skillId, SkillRank.Awakening) != null) {
                                skillTab.Skills.Remove(skillId);
                            }
                        }
                    }
                    session.Config.Skill.ResetSkills(SkillRank.Awakening);
                }

                session.Player.Value.Character.Job = job;
                session.Config.Skill.SkillInfo.SetJob(job);

                session.Player.Buffs.Buffs.Clear();
                session.Player.Buffs.Initialize();
                session.Player.Buffs.LoadFieldBuffs();
                session.Stats.Refresh();
                session.Field?.Broadcast(JobPacket.Advance(session.Player, session.Config.Skill.SkillInfo));
                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class InfoCommand : Command {
        private readonly GameSession session;

        public InfoCommand(GameSession session) : base("info", "Prints player info.") {
            this.session = session;

            this.SetHandler<InvocationContext>(Handle);
        }

        private void Handle(InvocationContext ctx) {
            ctx.Console.Out.WriteLine($"Player: {session.Player.ObjectId} ({session.PlayerName})");
            ctx.Console.Out.WriteLine($"  Position: {session.Player.Position}");
            ctx.Console.Out.WriteLine($"  Rotation: {session.Player.Rotation}");
        }
    }

    private class SkillPointCommand : Command {
        private readonly GameSession session;

        public SkillPointCommand(GameSession session) : base("skillpoint", "Add skill points to player.") {
            this.session = session;

            var points = new Argument<int>("points", "Skill points to add.");
            var rank = new Option<short>(["--rank", "-r"], () => 0, "Job rank to add points to. (0 for normal, 1 for awakening)");

            AddArgument(points);
            AddOption(rank);
            this.SetHandler<InvocationContext, int, short>(Handle, points, rank);
        }

        private void Handle(InvocationContext ctx, int points, short rank) {
            try {
                rank = (short) Math.Clamp((int) rank, 0, 1);
                session.Config.AddSkillPoint(SkillPointSource.Unknown, points, rank);
                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class CurrencyCommand : Command {
        private readonly GameSession session;

        public CurrencyCommand(GameSession session) : base("currency", "Add currency to player.") {
            this.session = session;

            var currency = new Argument<string>("currency", "Type of currency to add: meso, meret, valortoken, treva, rue, havifruit, reversecoin, mentortoken, menteetoken, starpoint, mesotoken.");
            var amount = new Argument<long>("amount", "Amount of currency to add.");

            AddArgument(currency);
            AddArgument(amount);
            this.SetHandler<InvocationContext, string, long>(Handle, currency, amount);
        }

        private void Handle(InvocationContext ctx, string currency, long amount) {
            try {
                switch (currency.ToLower()) {
                    // Handling meso and meret separately because they are not in the CurrencyType enum.
                    case "meso":
                        session.Currency.Meso += amount;
                        break;
                    case "meret":
                        session.Currency.Meret += amount;
                        break;
                    case "gamemeret":
                        session.Currency.GameMeret += amount;
                        break;
                    default:
                        if (!Enum.TryParse(currency, true, out CurrencyType currencyType)) {
                            ctx.Console.Error.WriteLine($"Failed to parse currency type: {currency}");
                            ctx.ExitCode = 1;
                            return;
                        }
                        session.Currency[currencyType] += amount;
                        break;
                }
                ctx.ExitCode = 0;
            } catch (SystemException ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }
    }

    private class DailyResetCommand : Command {
        private readonly GameSession session;

        public DailyResetCommand(GameSession session) : base("daily-reset", "Force daily reset for this player.") {
            this.session = session;
            this.SetHandler<InvocationContext>(Handle);
        }

        private void Handle(InvocationContext ctx) {
            session.DailyReset();
        }
    }

    private class InventoryCommand : Command {
        private readonly GameSession session;

        public InventoryCommand(GameSession session) : base("inventory", "Manage player inventory.") {
            this.session = session;

            AddCommand(new ClearInventoryCommand(session));
        }

        private class ClearInventoryCommand : Command {
            private readonly GameSession session;

            public ClearInventoryCommand(GameSession session) : base("clear", "Clear player inventory.") {
                this.session = session;

                var invTab = new Argument<string>("tab", $"Inventory tab to clear. One of: {string.Join(", ", Enum.GetNames(typeof(InventoryType)))}");

                AddArgument(invTab);
                this.SetHandler<InvocationContext, string>(Handle, invTab);
            }

            private void Handle(InvocationContext ctx, string tab) {
                if (!Enum.TryParse(tab, true, out InventoryType inventoryType)) {
                    ctx.Console.Error.WriteLine($"Invalid inventory tab: {tab}. Must be one of: {string.Join(", ", Enum.GetNames(typeof(InventoryType)))}");
                    ctx.ExitCode = 1;
                    return;
                }

                session.Item.Inventory.Clear(inventoryType);
                ctx.Console.Out.WriteLine($"Cleared {inventoryType} inventory.");
                ctx.ExitCode = 0;
            }
        }
    }

    private class SetGearCommand : Command {
        private readonly GameSession session;

        public SetGearCommand(GameSession session) : base("setgear", "Set player's gear based on build and class.") {
            this.session = session;

            var buildArg = new Argument<string>("build", "Name of the gear build.");
            var classArg = new Argument<string?>("class", "Class of the player") { Arity = ArgumentArity.ZeroOrOne };

            AddArgument(buildArg);
            AddArgument(classArg);
            this.SetHandler<InvocationContext, string, string?>(Handle, buildArg, classArg);
        }

        private void Handle(InvocationContext ctx, string buildName, string? className) {
            try {
                JobCode jobCode = string.IsNullOrEmpty(className) ? session.Player.Value.Character.Job.Code() : Enum.Parse<JobCode>(className, true);

                var gearSet = buildName switch {
                    "FirePrism" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13200308, 15000312, 15200311, 13300307, 15100304, 15300307, 13100313, 13400306, 15400293, 15500225, 15600227)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14100278, 0, 0, 14000269, 0, 0, 13100313, 13400306, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11301338 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11401141 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11501034 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11601222 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11701318 + JobOffset(job))),
                    "FirePrismArmor" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13200308, 15000312, 15200311, 13300307, 15100304, 15300307, 13100313, 13400306, 15400293, 15500225, 15600227)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14100278, 0, 0, 14000269, 0, 0, 13100313, 13400306, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11301338 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11401141 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11501034 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11601222 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11701318 + JobOffset(job))),
                    "FirePrismAccessories" => new GearSet(
                        new(EquipSlot.PD, 6, (job) => 11900127),
                        new(EquipSlot.EA, 6, (job) => 11200105),
                        new(EquipSlot.RI, 6, (job) => 12000116),
                        new(EquipSlot.MT, 6, (job) => 11800149),
                        new(EquipSlot.BE, 6, (job) => 12100117)),
                    "TairenRoyal" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13200309, 15000313, 15200312, 13300308, 15100305, 15300308, 13100314, 13400307, 15400294, 15500226, 15600228)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14100279, 0, 0, 14000270, 0, 0, 13100314, 13400307, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11301349 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11401152 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11501045 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11601233 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11701329 + JobOffset(job)),
                        new(EquipSlot.PD, 6, (job) => 11900121),
                        new(EquipSlot.EA, 6, (job) => 11200096),
                        new(EquipSlot.RI, 6, (job) => 12000110),
                        new(EquipSlot.MT, 6, (job) => 11800123),
                        new(EquipSlot.BE, 6, (job) => 12100111)),
                    "TairenRoyalArmor" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13200309, 15000313, 15200312, 13300308, 15100305, 15300308, 13100314, 13400307, 15400294, 15500226, 15600228)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14100279, 0, 0, 14000270, 0, 0, 13100314, 13400307, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11301349 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11401152 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11501045 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11601233 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11701329 + JobOffset(job))),
                    "TairenRoyalAccessories" => new GearSet(
                        new(EquipSlot.PD, 6, (job) => 11900121),
                        new(EquipSlot.EA, 6, (job) => 11200096),
                        new(EquipSlot.RI, 6, (job) => 12000110),
                        new(EquipSlot.MT, 6, (job) => 11800123),
                        new(EquipSlot.BE, 6, (job) => 12100111)),
                    "TairenOfficer" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200306, 15000310, 15200309, 13300305, 15100302, 15300305, 13100311, 13400304, 15400291, 15500223, 15600225)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100276, 0, 0, 14000267, 0, 0, 13100311, 13400304, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11301316 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11401119 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11501012 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11601200 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11701296 + JobOffset(job))),
                    "AntiqueInfernuke" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200307, 15000311, 15200310, 13300306, 15100303, 15300306, 13100312, 13400305, 15400292, 15500224, 15600226)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100277, 0, 0, 14000268, 0, 0, 13100312, 13400305, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11301327 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11401130 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11501023 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11601211 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11701307 + JobOffset(job))),
                    "TairenCombat" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13200305, 15000309, 15200308, 13300304, 15100301, 15300304, 13100310, 13400303, 15400290, 15500222, 15600224)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14100275, 0, 0, 14000266, 0, 0, 13100310, 13400303, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 4, (job) => 11301294 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11401097 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11500990 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11601178 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11701274 + JobOffset(job))),
                    "AntiqueArigon" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13200304, 15000308, 15200307, 13300303, 15100300, 15300303, 13100309, 13400302, 15400289, 15500221, 15600223)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14100274, 0, 0, 14000265, 0, 0, 13100309, 13400302, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 4, (job) => 11301283 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11401086 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11500979 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11601167 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11701263 + JobOffset(job))),
                    "Humanitas" => new GearSet(
                        new(EquipSlot.PD, 5, (job) => 11900120),
                        new(EquipSlot.EA, 5, (job) => 11200095),
                        new(EquipSlot.RI, 5, (job) => 12000109),
                        new(EquipSlot.MT, 5, (job) => 11800122),
                        new(EquipSlot.BE, 5, (job) => 12100110)),
                    "TairenRensha" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11900119),
                        new(EquipSlot.EA, 4, (job) => 11200094),
                        new(EquipSlot.RI, 4, (job) => 12000108),
                        new(EquipSlot.MT, 4, (job) => 11800121),
                        new(EquipSlot.BE, 4, (job) => 12100109)),
                    "KritiasNamed" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11900122), // Necklace
                        new(EquipSlot.CP, 5, (job) => 11300188), // Zakum Hat
                        new(EquipSlot.RI, 4, (job) => 12000111), // Zakum Ring
                        new(EquipSlot.MT, 4, (job) => 11860127), // Infernog Cape
                        new(EquipSlot.BE, 4, (job) => 12100112)), // Balrog Belt
                    "Soulrend" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13260294, 15060298, 15260297, 13360293, 15160290, 15360293, 13160299, 13460292, 15460178, 15560215, 15660219)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14160264, 0, 0, 14060255, 0, 0, 13160299, 13460292, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11361220 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11461038 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11560940 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11661121 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11761202 + JobOffset(job)),
                        new(EquipSlot.PD, 6, (job) => 11960114),
                        new(EquipSlot.EA, 6, (job) => 11260089),
                        new(EquipSlot.RI, 6, (job) => 12060103),
                        new(EquipSlot.MT, 6, (job) => 11860113),
                        new(EquipSlot.BE, 6, (job) => 12160104)),
                    "SoulrendArmor" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13260294, 15060298, 15260297, 13360293, 15160290, 15360293, 13160299, 13460292, 15460178, 15560215, 15660219)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14160264, 0, 0, 14060255, 0, 0, 13160299, 13460292, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11361220 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11461038 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11560940 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11661121 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11761202 + JobOffset(job))),
                    "SoulrendAccessories" => new GearSet(
                        new(EquipSlot.PD, 6, (job) => 11960114),
                        new(EquipSlot.EA, 6, (job) => 11260089),
                        new(EquipSlot.RI, 6, (job) => 12060103),
                        new(EquipSlot.MT, 6, (job) => 11860113),
                        new(EquipSlot.BE, 6, (job) => 12160104)),
                    "Fractured" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13260295, 15060299, 15260298, 13360294, 15160291, 15360294, 13160300, 13460293, 15460179, 15560216, 15660220)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14160265, 0, 0, 14060256, 0, 0, 13160300, 13460293, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11361231 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11461049 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11560951 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11661132 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11761213 + JobOffset(job)),
                        new(EquipSlot.PD, 6, (job) => 11260090),
                        new(EquipSlot.EA, 6, (job) => 11960115),
                        new(EquipSlot.RI, 6, (job) => 12060104),
                        new(EquipSlot.MT, 6, (job) => 11860114),
                        new(EquipSlot.BE, 6, (job) => 12160105)),
                    "FracturedArmor" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13260295, 15060299, 15260298, 13360294, 15160291, 15360294, 13160300, 13460293, 15460179, 15560216, 15660220)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14160265, 0, 0, 14060256, 0, 0, 13160300, 13460293, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11361231 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11461049 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11560951 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11661132 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11761213 + JobOffset(job))),
                    "FracturedAccessories" => new GearSet(
                        new(EquipSlot.PD, 6, (job) => 11260090),
                        new(EquipSlot.EA, 6, (job) => 11960115),
                        new(EquipSlot.RI, 6, (job) => 12060104),
                        new(EquipSlot.MT, 6, (job) => 11860114),
                        new(EquipSlot.BE, 6, (job) => 12160105)),
                    "Centurion" => new GearSet(
                        new(EquipSlot.PD, 5, (job) => 11960117),
                        new(EquipSlot.EA, 5, (job) => 11260092),
                        new(EquipSlot.RI, 5, (job) => 12060106),
                        new(EquipSlot.MT, 5, (job) => 11860117),
                        new(EquipSlot.BE, 5, (job) => 12160107)),
                    "Wayward" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11960116),
                        new(EquipSlot.EA, 4, (job) => 11260091),
                        new(EquipSlot.RI, 4, (job) => 12060105),
                        new(EquipSlot.MT, 4, (job) => 11860116),
                        new(EquipSlot.BE, 4, (job) => 12160106)),
                    "AwakeningNamed" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11900118), // Siren Necklace
                        new(EquipSlot.EA, 4, (job) => 11260096), // Madrakan Earring
                        new(EquipSlot.RI, 4, (job) => 12000107), // Frost Ring
                        new(EquipSlot.MT, 4, (job) => 11800080), // Madrakan Cape
                        new(EquipSlot.MT, 4, (job) => 11860122), // Ariel Cape
                        new(EquipSlot.BE, 4, (job) => 12100108)), // Blizzard Belt
                    "DarkVanguard" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13260293, 15060297, 15260296, 13360292, 15160289, 15360292, 13160298, 13460291, 15460177, 15560214, 15660218)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14160263, 0, 0, 14060254, 0, 0, 13160298, 13460291, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11361209 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11461027 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11560929 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11661110 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11761191 + JobOffset(job))),
                    "Enigma" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13260292, 15060296, 15260295, 13360291, 15160288, 15360291, 13160297, 13460290, 15460176, 15560213, 15660217)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14160262, 0, 0, 14060253, 0, 0, 13160297, 13460290, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11361198 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11461016 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11560918 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11661099 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11761180 + JobOffset(job))),
                    "Behemoth" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13260291, 15060295, 15260294, 13360290, 15160287, 15360290, 13160296, 13460289, 15460175, 15560212, 15660216)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14160261, 0, 0, 14060252, 0, 0, 13160296, 13460289, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11361187 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11461005 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11560907 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11661088 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11761169 + JobOffset(job))),
                    "Demonwing" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13260290, 15060294, 15260293, 13360289, 15160286, 15360289, 13160295, 13460288, 15460174, 15560211, 15660215)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14160260, 0, 0, 14060251, 0, 0, 13160295, 13460288, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 4, (job) => 11361176 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11460994 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11560896 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11661077 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11761158 + JobOffset(job))),
                    "Frontier" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13260289, 15060293, 15260292, 13360288, 15160285, 15360288, 13160294, 13460287, 15460173, 15560210, 15660214)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14160259, 0, 0, 14060250, 0, 0, 13160294, 13460287, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 4, (job) => 11361165 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11460983 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11560885 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11661066 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11761147 + JobOffset(job))),
                    "Tidemaster" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13260288, 15060292, 15260291, 13360287, 15160284, 15360287, 13160293, 13460286, 15460172, 15560209, 15660213)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14160258, 0, 0, 14060249, 0, 0, 13160293, 13460286, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 4, (job) => 11361154 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11460972 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11560874 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11661055 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11761136 + JobOffset(job))),
                    "Pluto" => new GearSet(
                        new(EquipSlot.PD, 5, (job) => 11900099),
                        new(EquipSlot.EA, 5, (job) => 11200072),
                        new(EquipSlot.RI, 5, (job) => 12000088),
                        new(EquipSlot.MT, 5, (job) => 11800092),
                        new(EquipSlot.BE, 5, (job) => 12100088)),
                    "Mars" => new GearSet(
                        new(EquipSlot.PD, 5, (job) => 11900100),
                        new(EquipSlot.EA, 5, (job) => 11200073),
                        new(EquipSlot.RI, 5, (job) => 12000089),
                        new(EquipSlot.MT, 5, (job) => 11800093),
                        new(EquipSlot.BE, 5, (job) => 12100089)),
                    "Absolute" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11930032),
                        new(EquipSlot.EA, 4, (job) => 11250113),
                        new(EquipSlot.RI, 4, (job) => 12030024),
                        new(EquipSlot.MT, 4, (job) => 11850170),
                        new(EquipSlot.BE, 4, (job) => 12130027)),
                    "ChaosNamed" => new GearSet(
                        new(EquipSlot.PD, 4, (job) => 11900071), // Kandura Necklace
                        new(EquipSlot.EA, 4, (job) => 11200052), // Nutaman Earring
                        new(EquipSlot.EA, 4, (job) => 11200006), // Balrog Earring
                        new(EquipSlot.CP, 4, (job) => 11300140), // Balrog Hat
                        new(EquipSlot.CP, 4, (job) => 11350744), // Varrekant Hat
                        new(EquipSlot.MT, 4, (job) => 11850175), // Varrekant Cape
                        new(EquipSlot.RI, 4, (job) => 12000050), // Pyrros Ring
                        new(EquipSlot.GL, 4, (job) => 11600301), // Pyrros Gloves
                        new(EquipSlot.BE, 5, (job) => 12100049), // Old Fairy King Belt
                        new(EquipSlot.BE, 4, (job) => 12100050)), // Old Fairy Belt
                    "ChaosUnreleasedNamed" => new GearSet(
                        new(EquipSlot.PD, 5, (job) => 11960118), // Peridot Necklace
                        new(EquipSlot.PD, 4, (job) => 11900088), // Black Covenant Necklace
                        new(EquipSlot.RI, 4, (job) => 12000078), // Conspirator Ring
                        new(EquipSlot.BE, 4, (job) => 12100077), // Shadow Scallion Belt
                        new(EquipSlot.PD, 4, (job) => 11900073), // Shuabritze Necklace
                        new(EquipSlot.RI, 4, (job) => 12000063), // Shuabritze Ring
                        new(EquipSlot.EA, 4, (job) => 11200057)), // Shuabritze Earring
                    "Eternal" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13200149, 15000153, 15200152, 13300148, 15100146, 15300146, 13100153, 13400146, 15400091, 15500065, 15600065)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14100134, 0, 0, 14000125, 0, 0, 13100153, 13400146, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => SelectItem(job, 11360137, 11360135, 11360148, 11360138, 11360146, 11360136, 11360143, 11360144, 11360432, 11360596, 11360643)),
                        new(EquipSlot.CL, 6, (job) => SelectItem(job, 11460075, 11460074, 11460914, 11460915, 11460078, 11460916, 11460076, 11460077, 11460387, 11460512, 11460556)),
                        new(EquipSlot.PA, 6, (job) => SelectItem(job, 11560076, 11560075, 11560818, 11560819, 11560079, 11560820, 11560077, 11560078, 11560315, 11560426, 11560472)),
                        new(EquipSlot.GL, 6, (job) => SelectItem(job, 11660086, 11660084, 11660093, 11660087, 11660091, 11660085, 11660088, 11660089, 11660390, 11660538, 11660581)),
                        new(EquipSlot.SH, 6, (job) => SelectItem(job, 11760107, 11760105, 11760115, 11760108, 11760113, 11760106, 11760109, 11760111, 11760417, 11760560, 11760611))),
                    "CrimsonBalrog" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200043, 15000041, 15200142, 13300138, 15100136, 15300136, 13100028, 13400136, 15400064, 15500064, 15600064)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100126, 0, 0, 14000118, 0, 0, 13100028, 13400136, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11301117 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11400957 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11500861 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11601030 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11701105 + JobOffset(job))),
                    "Reverse3" => new GearSet(
                        new(EquipSlot.RH, 6, (job) => SelectItem(job, 13260226, 15060230, 15260229, 13360225, 15160222, 15360225, 13160231, 13460224, 15460115, 15560153, 15660142)), // Weapon
                        new(EquipSlot.LH, 6, (job) => SelectItem(job, 14160194, 0, 0, 14060185, 0, 0, 13160231, 13460224, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 6, (job) => 11360917 + JobOffset(job)),
                        new(EquipSlot.CL, 6, (job) => 11460798 + JobOffset(job)),
                        new(EquipSlot.PA, 6, (job) => 11560705 + JobOffset(job)),
                        new(EquipSlot.GL, 6, (job) => 11660839 + JobOffset(job)),
                        new(EquipSlot.SH, 6, (job) => 11760909 + JobOffset(job))),
                    "Reverse2" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200227, 15000231, 15200230, 13300226, 15100223, 15300226, 13100232, 13400225, 15400116, 15500154, 15600143)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100195, 0, 0, 14000186, 0, 0, 13100232, 13400225, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11300928 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11400809 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11500716 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11600850 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11700920 + JobOffset(job))),
                    "Reverse" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200226, 15000230, 15200229, 13300225, 15100222, 15300225, 13100231, 13400224, 15400115, 15500153, 15600142)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100194, 0, 0, 14000185, 0, 0, 13100231, 13400224, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11300917 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11400798 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11500705 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11600839 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11700909 + JobOffset(job))),
                    "UnleashedNarubashan" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200190, 15000194, 15200193, 13300190, 15100186, 15300189, 13100195, 13400187, 15400093, 15500124, 15600118)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100164, 0, 0, 14000155, 0, 0, 13100195, 13400187, 0, 0, 0))), // Offhand
                    "LimitlessNarubashan" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200189, 15000193, 15200192, 13300189, 15100185, 15300188, 13100194, 13400186, 15400092, 15500123, 15600117)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100163, 0, 0, 14000154, 0, 0, 13100194, 13400186, 0, 0, 0))), // Offhand
                    "Might" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200188, 15000192, 15200191, 13300188, 15100184, 15300187, 13100193, 13400185, 15400253, 15500122, 15600207)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14130044, 0, 0, 14000030, 0, 0, 13100193, 13400185, 0, 0, 0))), // Offhand
                    "Fervor" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200191, 15000195, 15200194, 13300191, 15100187, 15300190, 13100196, 13400188, 15400094, 15500125, 15600119)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14100194, 0, 0, 14000185, 0, 0, 13100196, 13400188, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11300673 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11400580 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11500496 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11600607 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11700637 + JobOffset(job))),
                    "Lodestar" => new GearSet(
                        new(EquipSlot.CP, 5, (job) => 11351106 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11450946 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11550850 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11651019 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11751094 + JobOffset(job))),
                    "Papulatus" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13200091, 15000090, 0, 0, 0, 0, 0, 0, 0, 0, 0)), // Weapon
                        new(EquipSlot.EA, 4, (job) => 11200042),
                        new(EquipSlot.MT, 4, (job) => 11800076),
                        new(EquipSlot.BE, 4, (job) => 12100039),
                        new(EquipSlot.SH, 4, (job) => 11700218)),
                    "Panic" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13250294, 15050298, 15250297, 13350293, 15150290, 15350293, 13150299, 13450292, 15450293, 15550533, 15650534)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14150264, 0, 0, 14050255, 0, 0, 13150299, 13450292, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11350745 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11451016 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11550920 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11650674 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11750704 + JobOffset(job))),
                    "Extreme" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13250282, 15050286, 15250285, 13350281, 15150278, 15350281, 13150287, 13450280, 15450281, 15550521, 15650510)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14150252, 0, 0, 14050243, 0, 0, 13150287, 13450280, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11350684 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => SelectItem(job, 11450968, 11450973, 11450969, 11450970, 11450971, 11450972, 11450974, 11450975, 11450976, 11450977, 11450978)),
                        new(EquipSlot.PA, 5, (job) => SelectItem(job, 11550872, 11550877, 11550873, 11550874, 11550875, 11550876, 11550878, 11550879, 11550880, 11550881, 11550882)),
                        new(EquipSlot.GL, 5, (job) => 11650618 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11750648 + JobOffset(job))),
                    "Rage" => new GearSet(
                        new(EquipSlot.RH, 5, (job) => SelectItem(job, 13250282, 15050286, 15250285, 13350281, 15150278, 15350281, 13150287, 13450280, 15450281, 15550521, 15650510)), // Weapon
                        new(EquipSlot.LH, 5, (job) => SelectItem(job, 14150252, 0, 0, 14050243, 0, 0, 13150287, 13450280, 0, 0, 0)), // Offhand
                        new(EquipSlot.CP, 5, (job) => 11350695 + JobOffset(job)),
                        new(EquipSlot.CL, 5, (job) => 11450979 + JobOffset(job)),
                        new(EquipSlot.PA, 5, (job) => 11550883 + JobOffset(job)),
                        new(EquipSlot.GL, 5, (job) => 11650629 + JobOffset(job)),
                        new(EquipSlot.SH, 5, (job) => 11750659 + JobOffset(job))),
                    "Murpagoth" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13250283, 15050287, 15250286, 13350282, 15150279, 15350282, 13150288, 13450281, 15450283, 15550523, 15650524)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14150253, 0, 0, 14050244, 0, 0, 13150288, 13450281, 0, 0, 0))), // Offhand
                    "AncientRune" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13250284, 15050288, 15250287, 13350283, 15150280, 15350283, 13150289, 13450282, 15450284, 15550524, 15650525)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14150254, 0, 0, 14050245, 0, 0, 13150289, 13450282, 0, 0, 0))), // Offhand
                    "MSLOnyx" => new GearSet(
                        new(EquipSlot.RH, 4, (job) => SelectItem(job, 13250285, 15050289, 15250288, 13350284, 15150281, 15350284, 13150290, 13450283, 15450284, 15550524, 15650513)), // Weapon
                        new(EquipSlot.LH, 4, (job) => SelectItem(job, 14150255, 0, 0, 14050246, 0, 0, 13150290, 13450283, 0, 0, 0))), // Offhand
                    "Exquisite" => new GearSet(
                        new(EquipSlot.CP, 4, (job) => 11350728 + JobOffset(job)),
                        new(EquipSlot.CL, 4, (job) => 11451005 + JobOffset(job)),
                        new(EquipSlot.PA, 4, (job) => 11550909 + JobOffset(job)),
                        new(EquipSlot.GL, 4, (job) => 11650662 + JobOffset(job)),
                        new(EquipSlot.SH, 4, (job) => 11750692 + JobOffset(job))),
                    _ => throw new ArgumentException($"Invalid build name: {buildName}")
                };

                // Equip gear by manipulating the player's inventory and broadcasting changes
                Player player = session.Player;
                var equips = session.Item.Equips;

                foreach (var gear in gearSet.GearPieces) {
                    int itemId = gear.ItemSelector(jobCode);
                    if (itemId == 0) continue; // Skip if no valid item for this job
                    int rarity = gear.Rarity;
                    EquipSlot slot = gear.EquipSlot;

                    SpawnAndEquipItem(equips, player, slot, (itemId, rarity));
                }

                session.Stats.Refresh();

                ctx.Console.Out.WriteLine($"Successfully equipped {jobCode} with {buildName} gear.");
                ctx.ExitCode = 0;
            } catch (Exception ex) {
                ctx.Console.Error.WriteLine(ex.Message);
                ctx.ExitCode = 1;
            }
        }

        private void SpawnAndEquipItem(EquipManager equips, Player player, EquipSlot slot, (int itemId, int rarity) gear) {
            int itemId = gear.itemId;
            int rarity = gear.rarity;

            // Check if the slot already has an item and remove it if it does
            if (equips.Gear.TryGetValue(slot, out Item? existingItem)) {
                equips.Unequip(existingItem.Uid);
            }

            // Retrieve item metadata for the given item ID
            if (!session.ItemMetadata.TryGet(itemId, out ItemMetadata? itemMetadata)) {
                throw new InvalidOperationException($"Item metadata not found for item ID: {itemId}");
            }

            // Create new item with the specified rarity and roll maximum stats
            var newItem = session.Field.ItemDrop.CreateItem(itemId, rarity, rollMax: true);
            if (newItem == null) {
                throw new InvalidOperationException($"Failed to create item with ID: {itemId}");
            }

            // Apply specific stat lines to certain slots
            ApplySpecialStats(newItem, slot);

            using (GameStorage.Request db = session.GameStorage.Context()) {
                newItem = db.CreateItem(player.Character.Id, newItem);
                if (newItem == null) {
                    throw new InvalidOperationException($"Failed to create item in database with ID: {itemId}");
                }
            }

            if (!session.Item.Inventory.Add(newItem, true)) {
                throw new InvalidOperationException($"Failed to add item with ID: {itemId} to inventory");
            }

            // Equip the newly added item
            equips.Equip(newItem.Uid, slot, false);

            // Broadcast the equipped item using a general Field update packet
            session.Field?.Broadcast(FieldPacket.AddPlayer(session));
        }

        private void ApplySpecialStats(Item item, EquipSlot slot) {
            if (item.Stats == null) {
                // Initialize ItemStats with default empty options
                var basicOptions = new Dictionary<BasicAttribute, BasicOption>[ItemStats.TYPE_COUNT];
                var specialOptions = new Dictionary<SpecialAttribute, SpecialOption>[ItemStats.TYPE_COUNT];

                // Initialize each dictionary within the arrays
                for (int i = 0; i < ItemStats.TYPE_COUNT; i++) {
                    basicOptions[i] = new Dictionary<BasicAttribute, BasicOption>();
                    specialOptions[i] = new Dictionary<SpecialAttribute, SpecialOption>();
                }

                item.Stats = new ItemStats(basicOptions, specialOptions);
            }

            // Apply the appropriate stat based on the slot type
            if (slot == EquipSlot.CP || slot == EquipSlot.CL || slot == EquipSlot.PA || slot == EquipSlot.SH) {
                // Apply "Damage to Boss Enemies" stat to these armor slots
                ReplaceSpecialAttribute(item.Stats, ItemStats.Type.Random, SpecialAttribute.BossNpcDamage, GetMaxSpecialValue(SpecialAttribute.BossNpcDamage));
            } else if (slot == EquipSlot.GL) {
                // Determine piercing type based on the player's job/class
                if (IsPhysicalClass(session.Player.Value.Character.Job.Code())) {
                    ReplaceSpecialAttribute(item.Stats, ItemStats.Type.Random, SpecialAttribute.PhysicalPiercing, GetMaxSpecialValue(SpecialAttribute.PhysicalPiercing));
                } else {
                    ReplaceSpecialAttribute(item.Stats, ItemStats.Type.Random, SpecialAttribute.MagicalPiercing, GetMaxSpecialValue(SpecialAttribute.MagicalPiercing));
                }
            }
        }

        // Method to replace or update a special attribute in ItemStats, ensuring we only have 3 special attributes
        private void ReplaceSpecialAttribute(ItemStats stats, ItemStats.Type type, SpecialAttribute attribute, float value) {
            var option = stats[type];

            // If the attribute already exists, replace its value
            if (option.Special.ContainsKey(attribute)) {
                option.Special[attribute] = new SpecialOption(value, 0.0f);
            } else {
                // If the attribute does not exist and there are already 3 attributes, replace one
                if (option.Special.Count >= 3) {
                    // Remove a random existing attribute to make space for the new one
                    var keyToReplace = option.Special.Keys.First();
                    option.Special.Remove(keyToReplace);
                }

                // Now add the new attribute
                option.Special[attribute] = new SpecialOption(value, 0.0f);
            }
        }

        // Helper method to determine if the class is a physical class
        private bool IsPhysicalClass(JobCode job) {
            return job == JobCode.Knight ||
                   job == JobCode.Berserker ||
                   job == JobCode.Archer ||
                   job == JobCode.HeavyGunner ||
                   job == JobCode.Thief ||
                   job == JobCode.Assassin ||
                   job == JobCode.Striker;
        }

        // Method to get the max value for a given attribute
        private float GetMaxSpecialValue(SpecialAttribute attribute) {
            // Since we are using rollMax: true when creating the item,
            // we assume that the values are already rolled to max.
            // Here we simply use appropriate logic to get the maximum value.
            switch (attribute) {
                case SpecialAttribute.BossNpcDamage:
                    return 0.05f; // Example max value for "Damage to Boss Enemies"
                case SpecialAttribute.PhysicalPiercing:
                case SpecialAttribute.MagicalPiercing:
                    return 0.074f; // Example max value for both "Physical Piercing" and "Magic Piercing"
                default:
                    return 5.0f; // Default fallback value
            }
        }

        private static int JobOffset(JobCode job) {
            return job switch {
                JobCode.Knight => 0,
                JobCode.Berserker => 1,
                JobCode.Wizard => 2,
                JobCode.Priest => 3,
                JobCode.Archer => 4,
                JobCode.HeavyGunner => 5,
                JobCode.Thief => 6,
                JobCode.Assassin => 7,
                JobCode.RuneBlader => 8,
                JobCode.Striker => 9,
                JobCode.SoulBinder => 10,
                _ => throw new ArgumentException($"Invalid job code: {job}")
            };
        }

        private static int SelectItem(JobCode job, params int[] itemIds) {
            int index = JobOffset(job);
            return index < itemIds.Length ? itemIds[index] : 0;
        }

        private record GearSet(params GearPiece[] GearPieces);

        private record GearPiece(EquipSlot EquipSlot, int Rarity, Func<JobCode, int> ItemSelector);
    }
}
