using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;

namespace AdditionalPylons
{
    [ApiVersion(2, 1)]
    public class AdditionalPylonsPlugin : TerrariaPlugin
    {
        public override string Name => "AdditionalPylons";
        public override string Author => "ATSP / Updated for TShock 6.1";
        public override string Description => "Allows placing additional (infinite) pylons beyond Terraria's default limits.";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        private static readonly List<int> PylonItems = new()
        {
            ItemID.TeleportationPylonJungle,
            ItemID.TeleportationPylonPurity,
            ItemID.TeleportationPylonHallow,
            ItemID.TeleportationPylonUnderground,
            ItemID.TeleportationPylonSnow,
            ItemID.TeleportationPylonDesert,
            ItemID.TeleportationPylonOcean,
            ItemID.TeleportationPylonMushroom,
            ItemID.TeleportationPylonVictory,
            5652, // Underworld Pylon
            5653, // Aether Pylon
        };

        public AdditionalPylonsPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            GetDataHandlers.PlayerUpdate.Register(OnPlayerUpdate);
            GetDataHandlers.PlaceTileEntity.Register(OnPlaceTileEntity);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.PlayerUpdate.UnRegister(OnPlayerUpdate);
                GetDataHandlers.PlaceTileEntity.UnRegister(OnPlaceTileEntity);
            }
            base.Dispose(disposing);
        }

        private void OnPlayerUpdate(object? sender, GetDataHandlers.PlayerUpdateEventArgs e)
        {
            if (e.PlayerId != e.Player.Index)
                return;

            var item = e.Player.TPlayer.inventory[e.SelectedItem];

            // Victory and Aether pylons don't need the limit bypassed
            if (item.type == ItemID.TeleportationPylonVictory || item.type == 5653)
                return;

            if (e.Player.HasPermission("additionalpylons.inf")
                && PylonItems.Contains(item.type)
                && e.Player.TPlayer.controlUseItem)
            {
                ClearPylons();
            }
        }

        private static void ClearPylons()
        {
            try
            {
                // Use reflection to avoid referencing the private TeleportPylonsSystem type
                var pylonSystem = typeof(Main).GetProperty("PylonSystem")?.GetValue(null);
                if (pylonSystem == null) return;

                var pylonsField = pylonSystem.GetType().GetField("_pylons", BindingFlags.NonPublic | BindingFlags.Instance);
                if (pylonsField == null) return;

                var pylons = pylonsField.GetValue(pylonSystem) as IList;
                pylons?.Clear();
            }
            catch { }
        }

        private void OnPlaceTileEntity(object? sender, GetDataHandlers.PlaceTileEntityEventArgs e)
        {
            // Type 7 = TETeleportationPylon
            if (e.Type != 7)
                return;

            if (e.Player.HasPermission("additionalpylons.inf"))
            {
                TETeleportationPylon.Place(e.X, e.Y);
                NetMessage.SendData(
                    (int)PacketTypes.PlaceTileEntity,
                    -1,
                    e.Player.Index,
                    NetworkText.Empty,
                    e.X,
                    e.Y,
                    7);
                e.Handled = true;
            }
        }
    }
}
