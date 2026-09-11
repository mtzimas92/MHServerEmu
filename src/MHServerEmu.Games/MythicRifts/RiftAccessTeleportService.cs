using Gazillion;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.UI;

namespace MHServerEmu.Games.MythicRifts
{
    public static class RiftAccessTeleportService
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        // Hub NPCs for direct access to higher difficulty raid/patrol content.
        public static readonly PrototypeId OmegaPatrolTeleporterRef = (PrototypeId)1753661696525930987;
        public static readonly PrototypeId RaidAccessTeleporterRef = (PrototypeId)13792587214021661359;
        private static readonly PrototypeId UltronRaidTargetRef = (PrototypeId)6101407482858775734;
        private static readonly PrototypeId MidtownPatrolTargetCosmicRef = (PrototypeId)10267419782939942783;
        private static readonly PrototypeId ICPPatrolTargetCosmicRef = (PrototypeId)1301013882718790217;
        private static readonly PrototypeId HightownPatrolTargetCosmicRef = (PrototypeId)3715467830624070844;
        private static readonly PrototypeId Tier3SuperheroicRef = (PrototypeId)586640101754933627;
        private static readonly PrototypeId Tier5Omega1Ref = (PrototypeId)424700179461639950;
        private static readonly LocaleStringId RaidAccessDialogTextRef = (LocaleStringId)18000000000000080200;
        private static readonly LocaleStringId OmegaPatrolDialogTextRef = (LocaleStringId)18000000000000080201;
        private static readonly LocaleStringId OmegaPatrolLockedMessageRef = (LocaleStringId)18000000000000080202;
        private static readonly LocaleStringId CosmicAxisButtonRef = (LocaleStringId)18000000000000080210;
        private static readonly LocaleStringId OmegaMuspelheimButtonRef = (LocaleStringId)18000000000000080211;
        private static readonly LocaleStringId OmegaUltronButtonRef = (LocaleStringId)18000000000000080212;
        private static readonly LocaleStringId MoreRaidOptionsButtonRef = (LocaleStringId)18000000000000080213;
        private static readonly LocaleStringId OmegaMidtownButtonRef = (LocaleStringId)18000000000000080220;
        private static readonly LocaleStringId OmegaIndustryCityButtonRef = (LocaleStringId)18000000000000080221;
        private static readonly LocaleStringId OmegaHightownButtonRef = (LocaleStringId)18000000000000080222;
        private static readonly LocaleStringId MorePatrolOptionsButtonRef = (LocaleStringId)18000000000000080223;

        public static bool TryUseRiftAccessTeleporter(Player player, WorldEntity interactableObject)
        {
            if (player == null || interactableObject == null)
                return false;

            if (interactableObject.PrototypeDataRef == RaidAccessTeleporterRef)
            {
                UseRaidAccessTeleporter(player, interactableObject);
                return true;
            }

            if (interactableObject.PrototypeDataRef == OmegaPatrolTeleporterRef)
            {
                UseOmegaPatrolTeleporter(player, interactableObject);
                return true;
            }

            return false;
        }

        private static void UseRaidAccessTeleporter(Player player, WorldEntity npc)
        {
            Game game = player.Game;

            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnRaidAccessTeleporterDialogResponse;
            dialog.Message.LocaleString = RaidAccessDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, CosmicAxisButtonRef, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, MoreRaidOptionsButtonRef, ButtonStyle.SecondaryPositive, false);

            game.GameDialogManager.PostDialogToClient(dialog);

            void OnRaidAccessTeleporterDialogResponse(ulong playerGuid, DialogResponse response)
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    TeleportDialogPlayerToTarget(game, playerGuid, "Regions/RAIDS/AxisRaid/ConnectionNodes/AxisRaidEntryTarget.prototype", Tier3SuperheroicRef);
                    return;
                }

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                {
                    Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
                    if (responsePlayer != null)
                        ShowRaidAccessMoreTeleporter(responsePlayer, npc);
                }
            }
        }

        private static void ShowRaidAccessMoreTeleporter(Player player, WorldEntity npc)
        {
            Game game = player.Game;

            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnRaidAccessMoreTeleporterDialogResponse;
            dialog.Message.LocaleString = RaidAccessDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, OmegaMuspelheimButtonRef, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, OmegaUltronButtonRef, ButtonStyle.SecondaryPositive, false);

            game.GameDialogManager.PostDialogToClient(dialog);

            void OnRaidAccessMoreTeleporterDialogResponse(ulong playerGuid, DialogResponse response)
            {
                switch (response.ButtonIndex)
                {
                    case GameDialogResultEnum.eGDR_Option1:
                        TeleportDialogPlayerToTarget(game, playerGuid, "Regions/RAIDS/MuspelheimRaid/ConnectionNodes/SurturRaidEntryTarget.prototype", Tier5Omega1Ref);
                        break;

                    case GameDialogResultEnum.eGDR_Option2:
                        TeleportDialogPlayerToTarget(game, playerGuid, UltronRaidTargetRef, Tier5Omega1Ref);
                        break;
                }
            }
        }

        private static void UseOmegaPatrolTeleporter(Player player, WorldEntity npc)
        {
            if (HasOmegaPatrolAccessForCurrentAvatar(player) == false)
            {
                player.SendBannerMessage(OmegaPatrolLockedMessageRef);
                return;
            }

            Game game = player.Game;

            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnOmegaPatrolTeleporterDialogResponse;
            dialog.Message.LocaleString = OmegaPatrolDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, OmegaMidtownButtonRef, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, MorePatrolOptionsButtonRef, ButtonStyle.SecondaryPositive, false);

            game.GameDialogManager.PostDialogToClient(dialog);

            void OnOmegaPatrolTeleporterDialogResponse(ulong playerGuid, DialogResponse response)
            {
                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option1)
                {
                    TeleportDialogPlayerToTarget(game, playerGuid, MidtownPatrolTargetCosmicRef, Tier5Omega1Ref, TeleportContextEnum.TeleportContext_Waypoint);
                    return;
                }

                if (response.ButtonIndex == GameDialogResultEnum.eGDR_Option2)
                {
                    Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
                    if (responsePlayer != null)
                        ShowOmegaPatrolMoreTeleporter(responsePlayer, npc);
                }
            }
        }

        private static void ShowOmegaPatrolMoreTeleporter(Player player, WorldEntity npc)
        {
            Game game = player.Game;

            GameDialogInstance dialog = game.GameDialogManager.CreateInstance(player.DatabaseUniqueId);
            dialog.OnResponse = OnOmegaPatrolMoreTeleporterDialogResponse;
            dialog.Message.LocaleString = OmegaPatrolDialogTextRef;
            dialog.Options = DialogOptionEnum.WorldClick;
            dialog.TargetId = npc.Id;
            dialog.InteractorId = player.CurrentAvatar?.Id ?? Entity.InvalidId;
            dialog.AddButton(GameDialogResultEnum.eGDR_Option1, OmegaIndustryCityButtonRef, ButtonStyle.SecondaryPositive, false);
            dialog.AddButton(GameDialogResultEnum.eGDR_Option2, OmegaHightownButtonRef, ButtonStyle.SecondaryPositive, false);

            game.GameDialogManager.PostDialogToClient(dialog);

            void OnOmegaPatrolMoreTeleporterDialogResponse(ulong playerGuid, DialogResponse response)
            {
                PrototypeId targetRef = response.ButtonIndex switch
                {
                    GameDialogResultEnum.eGDR_Option1 => ICPPatrolTargetCosmicRef,
                    GameDialogResultEnum.eGDR_Option2 => HightownPatrolTargetCosmicRef,
                    _ => PrototypeId.Invalid
                };

                TeleportDialogPlayerToTarget(game, playerGuid, targetRef, Tier5Omega1Ref, TeleportContextEnum.TeleportContext_Waypoint);
            }
        }

        private static void TeleportDialogPlayerToTarget(Game game, ulong playerGuid, string targetPath, PrototypeId difficultyTierRef)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || difficultyTierRef == PrototypeId.Invalid)
                return;

            Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
            if (responsePlayer == null)
                return;

            PrototypeId targetRef = GameDatabase.GetPrototypeRefByName(targetPath);
            if (targetRef == PrototypeId.Invalid)
            {
                // Logger.Warn($"TeleportDialogPlayerToTarget(): Unable to find target {targetPath}.");
                return;
            }

            TeleportDialogPlayerToTarget(game, playerGuid, targetRef, difficultyTierRef);
        }

        private static void TeleportDialogPlayerToTarget(Game game, ulong playerGuid, PrototypeId targetRef, PrototypeId difficultyTierRef, TeleportContextEnum teleportContext = TeleportContextEnum.TeleportContext_Debug)
        {
            if (targetRef == PrototypeId.Invalid || difficultyTierRef == PrototypeId.Invalid)
                return;

            Player responsePlayer = game.EntityManager.GetEntityByDbGuid<Player>(playerGuid);
            if (responsePlayer == null)
                return;

            using var teleporterHandle = TeleporterPool.Get(out Teleporter teleporter);
            teleporter.Initialize(responsePlayer, teleportContext);
            teleporter.DifficultyTierRef = difficultyTierRef;

            if (teleporter.TeleportToTarget(targetRef) == false)
            {
                // Logger.Warn($"TeleportDialogPlayerToTarget(): Teleport failed for target {targetRef.GetNameFormatted()}.");
            }
        }

        public static bool HasOmegaPatrolAccessForCurrentAvatar(Player player)
        {
            return player?.MythicRiftProgress.HasOmegaPatrolAccess(player.CurrentAvatar) == true;
        }

        public static bool GrantOmegaPatrolAccessForCurrentAvatar(Player player)
        {
            Avatar avatar = player?.CurrentAvatar;
            if (avatar == null)
                return false;

            player.MythicRiftProgress.SetOmegaPatrolAccess(avatar);
            return true;
        }

    }
}
