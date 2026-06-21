using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.NetModules;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;
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
            // Terraria 1.4.5 new pylons
            ItemID.TeleportationPylonUnderworld, // 5652
            ItemID.TeleportationPylonAether,       // 5653
        };

        // Use reflection to access private _pylons field safely
        private static readonly FieldInfo? PylonsField = typeof(TeleportPylonsSystem).GetField(
            "_pylons",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public AdditionalPylonsPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            // TShock 6 uses .Register() / .UnRegister() instead of += / -=
            GetDataHandlers.PlayerUpdate.Register(OnPlayerUpdate);
            GetDataHandlers.PlaceTileEntity.Register(OnPlaceTileEntity);
            GetDataHandlers.SendTileRect.Register(OnSendTileRect);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.PlayerUpdate.UnRegister(OnPlayerUpdate);
                GetDataHandlers.PlaceTileEntity.UnRegister(OnPlaceTileEntity);
                GetDataHandlers.SendTileRect.UnRegister(OnSendTileRect);
            }
            base.Dispose(disposing);
        }

        private void OnPlayerUpdate(object? sender, GetDataHandlers.PlayerUpdateEventArgs e)
        {
            if (e.PlayerId != e.Player.Index)
                return;

            var item = e.Player.TPlayer.inventory[e.SelectedItem];

            // Victory and Aether pylons don't need the limit bypassed
            if (item.netID == ItemID.TeleportationPylonVictory || item.netID == ItemID.TeleportationPylonAether)
                return;

            if (e.Player.HasPermission("additionalpylons.inf")
                && PylonItems.Contains(item.netID)
                && e.Player.TPlayer.controlUseItem)
            {
                ClearPylons();
            }
        }

        private static void ClearPylons()
        {
            if (PylonsField != null)
            {
                var pylons = PylonsField.GetValue(Main.PylonSystem) as List<TeleportPylonInfo>;
                pylons?.Clear();
            }
            else
            {
                // Fallback if the field becomes public in a future update
                Main.PylonSystem._pylons?.Clear();
            }
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

        private void OnSendTileRect(object? sender, GetDataHandlers.SendTileRectEventArgs e)
        {
            // Pylons are placed in a 3x4 tile rect
            if (e.Width != 3 || e.Length != 4)
                return;

            try
            {
                // IMPORTANT: Do NOT dispose the BinaryReader, as it would close the underlying MemoryStream
                // that TShock still needs to process.
                var reader = new BinaryReader(e.Data);
                var tiles = new NetTile[e.Width, e.Length];

                for (int x = 0; x < e.Width; x++)
                    for (int y = 0; y < e.Length; y++)
                        tiles[x, y] = new NetTile(reader);

                for (int x = 0; x < e.Width; x++)
                {
                    for (int y = 0; y < e.Length; y++)
                    {
                        if (tiles[x, y].Type == TileID.TeleportationPylon)
                        {
                            if (e.Player.HasPermission("additionalpylons.inf"))
                            {
                                // Broadcast the placed tiles to all clients
                                TSPlayer.All.SendTileRect((short)e.TileX, (short)e.TileY, 3, 4);

                                // Notify clients about the new pylon
                                var pylonInfo = new TeleportPylonInfo
                                {
                                    XPosition = e.TileX + x,
                                    YPosition = e.TileY + y,
                                    Type = (TeleportPylonType)Main.tile[e.TileX + x, e.TileY + y].frameX
                                };

                                NetMessage.SendData(
                                    (int)PacketTypes.LoadNetModule,
                                    -1,
                                    -1,
                                    NetworkText.Empty,
                                    NetTeleportPylonModule.SerializePylonPlacements(pylonInfo)
                                );

                                e.Handled = true;
                            }
                            return;
                        }
                    }
                }
            }
            catch
            {
                // If NetTile parsing fails for any reason, let TShock handle the packet normally
            }
        }
    }
}
