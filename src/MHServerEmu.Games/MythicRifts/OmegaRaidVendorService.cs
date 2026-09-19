using MHServerEmu.Core.Memory;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.MythicRifts
{
    public static class OmegaRaidVendorService
    {
        private static readonly PrototypeId HelicarrierRegionRef = (PrototypeId)13623659297421268224UL;
        private static readonly PrototypeId VendorPrototypeRef = GameDatabase.GetPrototypeRefByName(
            "Entity/Characters/Vendors/Prototypes/Endgame/DangerRoomRewardsVendor.prototype");
        private const float CrafterSideOffset = 180f;

        public static bool IsOmegaRaidVendor(WorldEntity vendor)
        {
            return vendor?.Region?.PrototypeDataRef == HelicarrierRegionRef &&
                   vendor.PrototypeDataRef == VendorPrototypeRef;
        }

        public static void SpawnInHelicarrier(Region region)
        {
            if (region?.PrototypeDataRef != HelicarrierRegionRef || VendorPrototypeRef == PrototypeId.Invalid)
                return;

            WorldEntity crafter = null;
            foreach (Entity entity in region.Entities)
            {
                if (entity is not WorldEntity candidate || candidate.IsVendor == false)
                    continue;

                PrototypeId vendorTypeRef = candidate.Properties[PropertyEnum.VendorType];
                if (vendorTypeRef.As<VendorTypePrototype>()?.IsCrafter == true)
                {
                    crafter = candidate;
                    break;
                }
            }

            if (crafter == null)
                return;

            Vector3 crafterPosition = crafter.RegionLocation.Position;
            Orientation orientation = crafter.RegionLocation.Orientation;
            Vector3 vendorPosition = new(
                crafterPosition.X + MathF.Cos(orientation.Yaw) * CrafterSideOffset,
                crafterPosition.Y + MathF.Sin(orientation.Yaw) * CrafterSideOffset,
                crafterPosition.Z);

            using var settingsHandle = EntitySettingsPool.Get(out EntitySettings settings);
            settings.EntityRef = VendorPrototypeRef;
            settings.Position = vendorPosition;
            settings.Orientation = orientation;
            settings.RegionId = region.Id;
            region.Game.EntityManager.CreateEntity(settings);
        }
    }
}
