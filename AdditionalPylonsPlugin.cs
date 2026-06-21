using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Net;

namespace AdditionalPylons
{
    [ApiVersion(2, 1)]
    public class AdditionalPylonsPlugin : TerrariaPlugin
    {
        public override string Name => "AdditionalPylons";
        public override string Author => "ATSP / Updated for TShock 6.1";
        public override string Description => "Allows placing additional (infinite) pylons beyond Terraria's default limits.";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 0, 0);

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

        // Reflection field for clearing pylons
        private static readonly FieldInfo? PylonsField = typeof(Main).GetProperty("PylonSystem")?.PropertyType
            .GetField("_pylons", BindingFlags.NonPublic | BindingFlags.Instance);

        public AdditionalPylonsPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
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
                var pylonSystem = typeof(Main).GetProperty("PylonSystem")?.GetValue(null);
                if (pylonSystem == null) return;

                var pylons = PylonsField?.GetValue(pylonSystem) as IList;
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

        private void OnSendTileRect(object? sender, GetDataHandlers.SendTileRectEventArgs e)
        {
            // Pylons are placed in a 3x4 tile rect
            if (e.Width != 3 || e.Length != 4)
                return;

            try
            {
                // Read all tiles from the stream using NetTile.Unpack (OTAPI 3 API)
                var tiles = new NetTile[e.Width, e.Length];
                for (int x = 0; x < e.Width; x++)
                {
                    for (int y = 0; y < e.Length; y++)
                    {
                        tiles[x, y] = new NetTile();
                        tiles[x, y].Unpack(e.Data);
                    }
                }

                for (int x = 0; x < e.Width; x++)
                {
                    for (int y = 0; y < e.Length; y++)
                    {
                        if (tiles[x, y].Type == TileID.TeleportationPylon)
                        {
                            if (e.Player.HasPermission("additionalpylons.inf"))
                            {
                                // Broadcast the placed tiles to all clients
                                TSPlayer.All.SendTileRect((short)e.TileX, (short)e.TileY, (byte)e.Width, (byte)e.Length);

                                // Notify clients about the new pylon via NetTeleportPylonModule
                                try
                                {
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
                                }
                                catch
                                {
                                    // If TeleportPylonInfo or SerializePylonPlacements are unavailable,
                                    // vanilla will still register the pylon through PlaceTileEntity
                                }

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
