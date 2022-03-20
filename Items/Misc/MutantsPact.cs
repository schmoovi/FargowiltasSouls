using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FargowiltasSouls.Items.Misc
{
    public class MutantsPact : SoulsItem
    {
        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Mutant's Pact");
            Tooltip.SetDefault("Permanently reduces Mutant's shop prices by 30%\n" +
                "'This looks like a good venture for us'");
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.rare = ItemRarityID.Cyan;
            Item.maxStack = 99;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.consumable = true;
            Item.value = Item.sellPrice(0, 10);
        }

        public override bool CanUseItem(Player player)
        {
            return !player.GetModPlayer<FargoSoulsPlayer>().MutantsPact;
        }

        public override bool? UseItem(Player player)
        {
            if (player.itemAnimation > 0 && player.itemTime == 0)
            {
                player.GetModPlayer<FargoSoulsPlayer>().MutantsPact = true;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, (int)player.position.X, (int)player.position.Y, 0);
            }
            return true;
        }

        public override void SafeModifyTooltips(List<TooltipLine> list)
        {
            foreach (TooltipLine line2 in list)
            {
                if (line2.mod == "Terraria" && line2.Name == "ItemName")
                {
                    line2.overrideColor = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
                }
            }
        }
    }
}