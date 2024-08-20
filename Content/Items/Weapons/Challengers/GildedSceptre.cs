using FargowiltasSouls.Content.Bosses.CursedCoffin;
using FargowiltasSouls.Content.Items.BossBags;
using FargowiltasSouls.Content.Projectiles.ChallengerItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FargowiltasSouls.Content.Items.Weapons.Challengers
{
    public class GildedSceptre : SoulsItem
    {
        private int delay = 0;
        private bool lastLMouse = false;
        public override int NumFrames => 6;
        public override void SetStaticDefaults()
        {
            Terraria.GameContent.Creative.CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 1;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 6));
            ItemID.Sets.AnimatesAsSoul[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.damage = 22;
            Item.DamageType = DamageClass.Magic;
            Item.width = 56;
            Item.height = 56;
            Item.useTime = 9;
            Item.useAnimation = 9;
            Item.channel = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1f;
            Item.value = Item.sellPrice(0, 10);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = CursedCoffin.ShotSFX;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GildedSceptreProj>();
            Item.shootSpeed = 1f;
            Item.noMelee = true;
            Item.mana = 4;
        }
        public override void HoldItem(Player player)
        {
            if (lastLMouse && !Main.mouseLeft && delay == 0)
                delay = 50;
            if (delay > 0)
                delay--;
            if (delay == 1)
            {
                if (player.whoAmI == Main.myPlayer)
                {
                    //SoundEngine.PlaySound(new SoundStyle("FargowiltasSouls/Assets/Sounds/ChargeSound"), player.Center);
                }
                //dust
                double spread = 2 * Math.PI / 36;
                for (int i = 0; i < 36; i++)
                {
                    Vector2 velocity = new Vector2(2, 2).RotatedBy(spread * i);

                    int index2 = Dust.NewDust(player.Center, 0, 0, DustID.ShadowbeamStaff, velocity.X, velocity.Y, 100);
                    Main.dust[index2].noGravity = true;
                    Main.dust[index2].noLight = true;
                }
            }
            lastLMouse = Main.mouseLeft;

            FunnyCircle(player);
        }
        private void FunnyCircle(Player player)
        {
            const float Threshold = 0.165f; // Average error% to circle must be lower than this 
            IEnumerable<Projectile> shots = Main.projectile.Where(p => p.TypeAlive(Item.shoot) && p.owner == player.whoAmI && p.ai[2] != 1);
            if (!shots.Any())
                return;
            int shotCount = shots.Count();

            if (shotCount < 4)
            {
                if (Main.mouseLeftRelease)
                {
                    foreach (var shot in shots)
                    {
                        shot.Kill();
                    }
                }
                return;
            }
            Vector2 circleCenter = Vector2.Zero;
            foreach (var shot in shots)
            {
                circleCenter += shot.Center;
            }
            circleCenter /= shotCount;
            float circleRadius = 0;
            foreach (var shot in shots)
            {
                circleRadius += shot.Distance(circleCenter);
            }
            circleRadius /= shotCount;
            float error = 0;
            foreach (var shot in shots)
            {
                error += shot.Distance(circleCenter + circleCenter.DirectionTo(shot.Center) * circleRadius) / circleRadius;
            }
            error /= shotCount;
            if (error < Threshold)
            {
                if (Main.mouseLeftRelease)
                {
                    foreach (var shot in shots)
                    {
                        shot.ai[2] = 1;
                        shot.ai[0] = -1;
                        shot.velocity = shot.DirectionTo(circleCenter) * 18;
                        shot.netUpdate = true;
                    }
                    SoundEngine.PlaySound(CursedCoffin.BigShotSFX, circleCenter);
                }
            }
            else if (Main.mouseLeftRelease)
            {
                foreach (var shot in shots)
                {
                    shot.Kill();
                }
            }
        }
        public override Vector2? HoldoutOffset()
        {
            return new Vector2(0, 0f);
        }
        public override bool CanUseItem(Player player)
        {
            return delay <= 1 && !Main.projectile.Any(p => p.TypeAlive(Item.shoot) && p.owner == player.whoAmI && p.Distance(Main.MouseWorld) < 32) && !Collision.IsWorldPointSolid(Main.MouseWorld);
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 pos = Main.MouseWorld;
            Projectile.NewProjectile(source, pos.X, pos.Y, 0f, 0f, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes()
        {
            //CreateRecipe().AddIngredient<LifelightBag>(2).AddTile(TileID.Solidifier).DisableDecraft().Register();
        }
    }
}