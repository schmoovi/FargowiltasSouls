﻿using FargowiltasSouls.Content.Projectiles.Minions;
using FargowiltasSouls.Core.AccessoryEffectSystem;
using FargowiltasSouls.Core.Toggler.Content;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static FargowiltasSouls.Content.Items.Accessories.Forces.TimberForce;

namespace FargowiltasSouls.Content.Items.Accessories.Enchantments
{
    public class PalmWoodEnchant : BaseEnchant
    {
        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            // DisplayName.SetDefault("Palm Wood Enchantment");
            /* Tooltip.SetDefault(
@"Double tap down to spawn a palm tree sentry that throws nuts at enemies
'Alarmingly calm'"); */

            //attack rate and damage increased, you can spawn 2 additional trees
        }

        public override Color nameColor => new(183, 141, 86);


        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare = ItemRarityID.Blue;
            Item.value = 10000;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.AddEffect<PalmwoodEffect>(Item);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.PalmWoodHelmet)
                .AddIngredient(ItemID.PalmWoodBreastplate)
                .AddIngredient(ItemID.PalmWoodGreaves)
                .AddIngredient(ItemID.Coral)
                .AddIngredient(ItemID.Banana)
                .AddIngredient(ItemID.Coconut)

            .AddTile(TileID.DemonAltar)
            .Register();
        }
    }
    public class PalmwoodEffect : AccessoryEffect
    {

        public override Header ToggleHeader => Header.GetHeader<TimberHeader>();
        public override int ToggleItemType => ModContent.ItemType<PalmWoodEnchant>();
        public override bool MinionEffect => true;
        public override void OnHitNPCEither(Player player, NPC target, NPC.HitInfo hitInfo, DamageClass damageClass, int baseDamage, Projectile projectile, Item item)
        {
            if (player.HasEffect<TimberEffect>())
            {
                if (player.FargoSouls().PalmWoodForceCD <= 0 && Collision.CanHit(player.Center, 0, 0, target.Center, 0, 0))
                {
                    Vector2 velocity = Vector2.Normalize(target.Center - player.Center) * 10;

                    int p = Projectile.NewProjectile(GetSource_EffectItem(player), player.Center, velocity, ProjectileID.SeedlerNut, (int)(hitInfo.SourceDamage * 1.5f), 2, player.whoAmI);
                    if (p != Main.maxProjectiles)
                        Main.projectile[p].DamageType = DamageClass.Summon;

                    player.FargoSouls().PalmWoodForceCD = 90;
                }
            }
        }
        public static void ActivatePalmwoodSentry(Player player)
        {
            if (player.HasEffect<PalmwoodEffect>() && !player.HasEffect<TimberEffect>())
            {
                if (player.whoAmI == Main.myPlayer)
                {
                    FargoSoulsPlayer modPlayer = player.FargoSouls();
                    bool forceEffect = modPlayer.ForceEffect<PalmWoodEnchant>();

                    Vector2 mouse = Main.MouseWorld;

                    int maxSpawn = 1;

                    if (player.ownedProjectileCounts[ModContent.ProjectileType<PalmTreeSentry>()] > maxSpawn - 1)
                    {
                        for (int i = 0; i < Main.maxProjectiles; i++)
                        {
                            Projectile proj = Main.projectile[i];

                            if (proj.active && proj.type == ModContent.ProjectileType<PalmTreeSentry>() && proj.owner == player.whoAmI)
                            {
                                proj.Kill();
                                break;
                            }
                        }
                    }

                    Vector2 offset = forceEffect ? (-40 * Vector2.UnitX) + (-120 * Vector2.UnitY) : (-41 * Vector2.UnitY);
                    FargoSoulsUtil.NewSummonProjectile(player.GetSource_Misc(""), mouse + offset, Vector2.Zero, ModContent.ProjectileType<PalmTreeSentry>(), forceEffect ? 100 : 15, 0f, player.whoAmI);
                }
            }
        }
    }
}
