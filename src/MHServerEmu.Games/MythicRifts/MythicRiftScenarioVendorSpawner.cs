using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.MythicRifts
{
    public static class MythicRiftScenarioVendorSpawner
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        // MoiraMacTaggertXMHealer.prototype is baked into the Danger Room hub cell, so the scenario vendor is
        // spawned beside her runtime position while patch data hides her.
        private static readonly PrototypeId MoiraMacTaggertXMHealerRef = (PrototypeId)10965963107371852253;
        private static readonly PrototypeId DangerRoomScenarioVendorRef = (PrototypeId)9925076672486449187;
        private const float DangerRoomScenarioVendorSideOffset = 150f;

        public static void SpawnDangerRoomScenarioVendor(Region region)
        {
            if (region == null)
                return;

            WorldEntity moira = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity.PrototypeDataRef == MoiraMacTaggertXMHealerRef && entity is WorldEntity worldEntity)
                {
                    moira = worldEntity;
                    break;
                }
            }

            if (moira == null)
            {
                Logger.Warn("SpawnDangerRoomScenarioVendor(): Failed to find Moira MacTaggert in the Danger Room hub");
                return;
            }

            Vector3 moiraPosition = moira.RegionLocation.Position;
            Orientation moiraOrientation = moira.RegionLocation.Orientation;
            float yaw = moiraOrientation.Yaw;
            Vector3 vendorPosition = new(moiraPosition.X + (MathF.Cos(yaw) * DangerRoomScenarioVendorSideOffset), moiraPosition.Y, moiraPosition.Z);

            using var entitySettingsHandle = EntitySettingsPool.Get(out EntitySettings entitySettings);
            entitySettings.EntityRef = DangerRoomScenarioVendorRef;
            entitySettings.Position = vendorPosition;
            entitySettings.Orientation = moiraOrientation;
            entitySettings.RegionId = region.Id;

            if (region.Game.EntityManager.CreateEntity(entitySettings) == null)
                Logger.Warn("SpawnDangerRoomScenarioVendor(): Failed to create the Danger Room Scenario Vendor entity");
        }
    }
}
