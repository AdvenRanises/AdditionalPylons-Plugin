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
    public class AdditionalPylons : TerrariaPlugin
    {
        public override string Name => "AdditionalPylons";
        public override string Author => "ATSP / Updated for TShock 6.1";
        public override string Description => "Allows placing additional (infinite) pylons beyond Terraria's default limits.";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        private static readonly List<int> Pylons = new List<int>
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

        public AdditionalPylons(Main game) : base(game)
        {
        }

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

        private void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs e)
        {
            if (e.PlayerId != e.Player.Index)
                return;

            if (e.Player.TPlayer.inventory[e.SelectedItem].netID == ItemID.TeleportationPylonVictory
                || e.Player.TPlayer.inventory[e.SelectedItem].netID == ItemID.TeleportationPylonAether)
            {
                return;
            }

            if (e.Player.HasPermission("additionalpylons.inf")
                && Pylons.Contains(e.Player.TPlayer.inventory[e.SelectedItem].netID)
                && e.Player.TPlayer.controlUseItem)
            {
                Main.PylonSystem._pylons.Clear();
            }
        }

        private void OnPlaceTileEntity(object sender, GetDataHandlers.PlaceTileEntityEventArgs e)
        {
            if (e.Type != 7)
                return;

            if (e.Player.HasPermission("additionalpylons.inf"))
            {
                TETeleportationPylon.Place(e.X, e.Y);
                NetMessage.SendData((int)PacketTypes.PlaceTileEntity, -1, e.Player.Index, NetworkText.Empty, e.X, e.Y, 7);
                e.Handled = true;
            }
        }

        private void OnSendTileRect(object sender, GetDataHandlers.SendTileRectEventArgs e)
        {
            if (e.Width == 3 && e.Length == 4)
            {
                // NOTE: If NetTile doesn't accept MemoryStream directly in your OTAPI build,
                // change this to: new NetTile(new BinaryReader(e.Data))
                NetTile[,] tiles = new NetTile[e.Width, e.Length];
                for (int x = 0; x < e.Width; ++x)
                {
                    for (int y = 0; y < e.Length; ++y)
                    {
                        tiles[x, y] = new NetTile(e.Data);
                    }
                }

                for (int x = 0; x < e.Width; ++x)
                {
                    for (int y = 0; y < e.Length; ++y)
                    {
                        if (tiles[x, y].Type == TileID.TeleportationPylon)
                        {
                            if (e.Player.HasPermission("additionalpylons.inf"))
                            {
                                TShockAPI.TSPlayer.All.SendTileRect((short)e.TileX, (short)e.TileY, 3, 4);

                                NetMessage.SendData(
                                    (int)PacketTypes.LoadNetModule,
                                    -1,
                                    -1,
                                    NetworkText.Empty,
                                    Terraria.GameContent.NetModules.NetTeleportPylonModule.SerializePylonPlacements(
                                        new Terraria.GameContent.TeleportPylonInfo
                                        {
                                            XPosition = e.TileX + x,
                                            YPosition = e.TileY + y,
                                            Type = (TeleportPylonType)Main.tile[e.TileX + x, e.TileY + y].frameX
                                        }
                                    )
                                );
                                e.Handled = true;
                            }
                            return;
                        }
                    }
                }
            }
        }
    }
}
