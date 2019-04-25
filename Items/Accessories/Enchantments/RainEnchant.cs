﻿using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FargowiltasSouls.Items.Accessories.Enchantments
{
    public class RainEnchant : ModItem
    {
    public override bool Autoload(ref string name)
        {
            return false;
        }
        
        public override string Texture => "FargowiltasSouls/Items/Placeholder";
        private readonly Mod thorium = ModLoader.GetMod("ThoriumMod");

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Rain Enchantment");
            Tooltip.SetDefault(
@"''
A miniature storm may appear when an enemy dies");
        }

        public override void SetDefaults()
        {
            item.width = 20;
            item.height = 20;
            item.accessory = true;
            ItemID.Sets.ItemNoGravity[item.type] = true;
            item.rare = 7;
            item.value = 100000;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            
        }

        public override void AddRecipes()
        {
            ModRecipe recipe = new ModRecipe(mod);

            recipe.AddIngredient(ItemID.RainHat);
            recipe.AddIngredient(ItemID.RainCoat);
            recipe.AddIngredient(ItemID.RainCloud);
            recipe.AddIngredient(ItemID.Umbrella);
            recipe.AddIngredient(ItemID.UmbrellaHat);
            recipe.AddIngredient(ItemID.NimbusRod);
            //

            recipe.AddTile(TileID.CrystalBall);
            recipe.SetResult(this);
            recipe.AddRecipe();
        }
    }
}
