using FargowiltasSouls.Common.Utilities;
using FargowiltasSouls.Content.Buffs.Masomode;
using FargowiltasSouls.Content.Projectiles;
using FargowiltasSouls.Content.Projectiles.Deathrays;
using FargowiltasSouls.Content.Projectiles.Masomode;
using FargowiltasSouls.Core.Globals;
using FargowiltasSouls.Core.NPCMatching;
using FargowiltasSouls.Core.Systems;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FargowiltasSouls.Content.Bosses.VanillaEternity
{
    public class Retinazer : EModeNPCBehaviour
    {
        public override NPCMatcher CreateMatcher() => new NPCMatcher().MatchType(NPCID.Retinazer);

        public int DeathrayState;
        public int LaserSide;
        public int AuraRadiusCounter;
        public int MechElectricOrbTimer;

        public bool StoredDirectionToPlayer;

        public bool Phase2;
        public bool DroppedSummon;

        public bool HasSaidEndure;
        public bool Resist;
        public int RespawnTimer;


        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write7BitEncodedInt(LaserSide);
            binaryWriter.Write7BitEncodedInt(DeathrayState);
            binaryWriter.Write7BitEncodedInt(AuraRadiusCounter);
            binaryWriter.Write7BitEncodedInt(MechElectricOrbTimer);
            bitWriter.WriteBit(StoredDirectionToPlayer);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            LaserSide = binaryReader.Read7BitEncodedInt();
            DeathrayState = binaryReader.Read7BitEncodedInt();
            AuraRadiusCounter = binaryReader.Read7BitEncodedInt();
            MechElectricOrbTimer = binaryReader.Read7BitEncodedInt();
            StoredDirectionToPlayer = bitReader.ReadBit();
        }

        public override void SetDefaults(NPC npc)
        {
            base.SetDefaults(npc);

            npc.lifeMax = (int)(npc.lifeMax * 1.2);
        }

        public override void OnFirstTick(NPC npc)
        {
            base.OnFirstTick(npc);

            npc.buffImmune[BuffID.Suffocation] = true;
        }
        public override bool SafePreAI(NPC npc)
        {
            EModeGlobalNPC.retiBoss = npc.whoAmI;

            Resist = false;

            if (WorldSavingSystem.SwarmActive)
                return true;

            //have some dr during phase transition animation
            if (npc.ai[0] == 1 || npc.ai[0] == 2)
                Resist = true;

            NPC spazmatism = FargoSoulsUtil.NPCExists(EModeGlobalNPC.spazBoss, NPCID.Spazmatism);

            if (WorldSavingSystem.MasochistModeReal && spazmatism == null && npc.HasValidTarget && ++RespawnTimer > 600)
            {
                RespawnTimer = 0;
                if (FargoSoulsUtil.HostCheck)
                {
                    int n = FargoSoulsUtil.NewNPCEasy(npc.GetSource_FromThis(), npc.Center + new Vector2(Main.rand.NextFloat(-1000, 1000), Main.rand.NextFloat(-800, -600)), NPCID.Spazmatism, target: npc.target);
                    if (n != Main.maxNPCs)
                    {
                        Main.npc[n].life = Main.npc[n].lifeMax / 4;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, number: n);
                        FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsRevive", new Color(175, 75, 255), Main.npc[n].FullName);
                    }
                }
            }
            
            if (!Phase2) //start phase 2
            {
                if (npc.GetLifePercent() < 0.66f || (spazmatism != null && spazmatism.GetLifePercent() < 0.66f))
                {
                    Phase2 = true;
                    npc.ai[0] = 1f;
                    npc.ai[1] = 0.0f;
                    npc.ai[2] = 0.0f;
                    npc.ai[3] = 0.0f;
                    npc.netUpdate = true;
                }
            }
            

            if (npc.life <= npc.lifeMax / 2 || npc.dontTakeDamage)
            {
                npc.dontTakeDamage = npc.life == 1 || !npc.HasValidTarget;
                if (npc.life != 1 && npc.HasValidTarget)
                    npc.dontTakeDamage = false;
                //become vulnerable again when both twins at low life
                if (npc.dontTakeDamage && npc.HasValidTarget && (spazmatism == null || spazmatism.life == 1))
                    npc.dontTakeDamage = false;
            }

            if (Main.dayTime && !Main.remixWorld)
            {
                if (npc.velocity.Y > 0)
                    npc.velocity.Y = 0;

                npc.velocity.Y -= 0.5f;
                npc.dontTakeDamage = true;

                if (spazmatism != null)
                {
                    if (npc.timeLeft < 60)
                        npc.timeLeft = 60;

                    if (spazmatism.timeLeft < 60)
                        spazmatism.timeLeft = 60;

                    npc.TargetClosest(false);
                    spazmatism.TargetClosest(false);
                    if (npc.Distance(Main.player[npc.target].Center) > 2000 && spazmatism.Distance(Main.player[spazmatism.target].Center) > 2000)
                    {
                        if (FargoSoulsUtil.HostCheck)
                        {
                            npc.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            spazmatism.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, EModeGlobalNPC.spazBoss);
                        }
                    }
                }

                return true;
            }
            if (!Phase2) // phase 1
            {

                ref float ai_State = ref npc.ai[1];

                if (!npc.HasPlayerTarget)
                    return true;
                Player player = Main.player[npc.target];

                switch (ai_State)
                {
                    case 0: //normal laser state
                        {

                            // movement
                            float num420 = 0.22f;
                            int num421 = 1;
                            if (npc.position.X + (float)(npc.width / 2) < player.position.X + (float)player.width)
                            {
                                num421 = -1;
                            }

                            Vector2 vector43 = new Vector2(npc.position.X + (float)npc.width * 0.5f, npc.position.Y + (float)npc.height * 0.5f);
                            float num422 = player.position.X + (float)(player.width / 2) + (float)(num421 * 300) - vector43.X;
                            float num423 = player.position.Y + (float)(player.height / 2) - 300f - vector43.Y;
                            if (npc.velocity.X < num422)
                            {
                                npc.velocity.X += num420;
                                if (npc.velocity.X < 0f && num422 > 0f)
                                {
                                    npc.velocity.X += num420 * 2;
                                }
                            }
                            else if (npc.velocity.X > num422)
                            {
                                npc.velocity.X -= num420;
                                if (npc.velocity.X > 0f && num422 < 0f)
                                {
                                    npc.velocity.X -= num420 * 2;
                                }
                            }
                            if (npc.velocity.Y < num423)
                            {
                                npc.velocity.Y += num420;
                                if (npc.velocity.Y < 0f && num423 > 0f)
                                {
                                    npc.velocity.Y += num420 * 2;
                                }
                            }
                            else if (npc.velocity.Y > num423)
                            {
                                npc.velocity.Y -= num420;
                                if (npc.velocity.Y > 0f && num423 < 0f)
                                {
                                    npc.velocity.Y -= num420 * 2;
                                }
                            }

                            // reworked p1 lasers
                            float delay = 55f; //LaserSide == 0 ? 50f : 20f;
                            if (npc.ai[3] >= delay)
                            {
                                npc.ai[3] = 0f;
                                Vector2 shootPos = new Vector2(npc.position.X + (float)npc.width * 0.5f, npc.position.Y + (float)npc.height * 0.5f);
                                float targetX = player.Center.X - shootPos.X;
                                float targetY = player.Center.Y - shootPos.Y;
                                if (FargoSoulsUtil.HostCheck)
                                {
                                    float num429 = 10.5f;
                                    int attackDamage_ForProjectiles3 = npc.GetAttackDamage_ForProjectiles(20f, 19f);
                                    int num430 = 83;

                                    float angle = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
                                    angle = num429 / angle;
                                    targetX *= angle;
                                    targetY *= angle;
                                    // terrible vanilla random
                                    //targetX += (float)Main.rand.Next(-40, 41) * 0.08f;
                                    //targetY += (float)Main.rand.Next(-40, 41) * 0.08f;

                                    targetX *= 0.6f;
                                    targetY *= 0.6f;
                                    Vector2 vel = new(targetX, targetY);
                                    shootPos.X += targetX * 15f;
                                    shootPos.Y += targetY * 15f;
                                    for (int i = -1; i <= 1; i++)
                                    {
                                        if (i == 0 && !WorldSavingSystem.MasochistModeReal)
                                            continue;
                                        float offset = i;
                                        offset *= (int)npc.HorizontalDirectionTo(player.Center);
                                        Vector2 vel2 = vel.RotatedBy(MathHelper.PiOver2 * 0.15f * offset);
                                        Projectile.NewProjectile(npc.GetSource_FromThis(), shootPos.X, shootPos.Y, vel2.X, vel2.Y, num430, attackDamage_ForProjectiles3, 0f, Main.myPlayer);
                                    }
 

                                }
                            }
                        }
                        break;
                    case 1: // dash frame
                        {
                            
                        }
                        break;
                    case 2: // mid dash
                        {
                            if (npc.ai[2] == 1f)
                                npc.velocity += npc.velocity.SafeNormalize(Vector2.UnitX) * npc.ai[3] * 3f;
                            npc.velocity = npc.velocity.RotateTowards(npc.DirectionTo(player.Center).ToRotation(), 0.02f);
                        }
                        break;
                }

            }
            else
            {
                npc.localAI[1] -= 1f;
                if (npc.localAI[1] >= (npc.ai[1] == 0 ? 170 : 50)) //hijacking vanilla laser code
                {
                    npc.localAI[1] = 0;
                    Vector2 vel = npc.SafeDirectionTo(Main.player[npc.target].Center);
                    Vector2 shotVel = vel;
                    int spread = 0;
                    int type = ModContent.ProjectileType<MechElectricOrbTwins>();
                    if (npc.ai[1] == 0)
                    {
                        spread = 1;
                        shotVel *= 20;
                        type = ModContent.ProjectileType<MechElectricOrb>();
                    }
                        
                    for (int i = -spread; i <= spread; i++)
                    {
                        Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center + (npc.width - 24) * vel, shotVel.RotatedBy(MathHelper.PiOver2 * 0.4f * i), type, FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer, npc.target, ai2: MechElectricOrb.Yellow);
                    }
                    
                }

                if (npc.HasPlayerTarget && (DeathrayState == 0 || DeathrayState == 3)) // normal AI
                {
                    Player player = Main.player[npc.target];
                    if (npc.ai[1] == 0f) // normal slow lasers
                    {

                        // movement
                        float num438 = 12.5f;
                        float num439 = 0.22f;

                        Vector2 vector45 = npc.Center;
                        float num440 = player.Center.X - npc.Center.X;
                        float num441 = player.Center.Y - npc.Center.Y - 400f;
                        float num442 = (float)Math.Sqrt(num440 * num440 + num441 * num441);
                        num442 = num438 / num442;
                        num440 *= num442;
                        num441 *= num442;
                        if (npc.velocity.X < num440)
                        {
                            npc.velocity.X += num439;
                            if (npc.velocity.X < 0f && num440 > 0f)
                            {
                                npc.velocity.X += num439 * 2;
                            }
                        }
                        else if (npc.velocity.X > num440)
                        {
                            npc.velocity.X -= num439;
                            if (npc.velocity.X > 0f && num440 < 0f)
                            {
                                npc.velocity.X -= num439 * 2;
                            }
                        }
                        if (npc.velocity.Y < num441)
                        {
                            npc.velocity.Y += num439;
                            if (npc.velocity.Y < 0f && num441 > 0f)
                            {
                                npc.velocity.Y += num439 * 2;
                            }
                        }
                        else if (npc.velocity.Y > num441)
                        {
                            npc.velocity.Y -= num439;
                            if (npc.velocity.Y > 0f && num441 < 0f)
                            {
                                npc.velocity.Y -= num439 * 2;
                            }
                        }
                    }
                }
                if (npc.ai[0] < 4f) //going to phase 3
                {
                    if (npc.life <= npc.lifeMax / 2 || spazmatism.life <= spazmatism.lifeMax / 2)
                    {
                        //npc.ai[0] = 4f;
                        npc.ai[0] = 604f; //initiate spin immediately
                        npc.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                        if (FargoSoulsUtil.HostCheck)
                            Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRingHollow>(), 0, 0f, Main.myPlayer, 11, npc.whoAmI);
                    }
                }
                else //in phase 3
                {

                    if (WorldSavingSystem.MasochistModeReal && spazmatism == null && --MechElectricOrbTimer < 0) //when twin dead, begin shooting Electric Orbs
                    {
                        MechElectricOrbTimer = 240;
                        if (FargoSoulsUtil.HostCheck && npc.HasPlayerTarget)
                        {
                            Vector2 distance = Main.player[npc.target].Center - npc.Center;
                            distance.Normalize();
                            distance *= 10f;
                            for (int i = 0; i < 12; i++)
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, distance.RotatedBy(2 * Math.PI / 12 * i),
                                    ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 0.8f), 0f, Main.myPlayer, ai2: MechElectricOrb.Yellow);
                        }
                    }

                    //dust code
                    if (DeathrayState != 0 && DeathrayState != 3 && Main.rand.Next(4) < 3)
                    {
                        int dustID = DeathrayState != 0 ? DustID.GemRuby : DustID.GemAmber;
                        int dust = Dust.NewDust(npc.position - new Vector2(2f, 2f), npc.width + 4, npc.height + 4, dustID, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f, 100, default, 3.5f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity *= 1.8f;
                        Main.dust[dust].velocity.Y -= 0.5f;
                        if (Main.rand.NextBool(4))
                        {
                            Main.dust[dust].noGravity = false;
                            Main.dust[dust].scale *= 0.5f;
                        }
                    }

                    if (DeathrayState == 0 || DeathrayState == 3) //not doing deathray, grow arena
                    {
                        AuraRadiusCounter--;
                        if (AuraRadiusCounter < 0)
                            AuraRadiusCounter = 0;
                    }
                    else //doing deathray, shrink arena
                    {
                        AuraRadiusCounter++;
                        if (AuraRadiusCounter > 180)
                            AuraRadiusCounter = 180;
                    }

                    float auraDistance = 2000 - 1200 * AuraRadiusCounter / 180f;
                    if (WorldSavingSystem.MasochistModeReal)
                        auraDistance *= 0.75f;
                    if (auraDistance < 2000 - 1)
                    {
                        EModeGlobalNPC.Aura(npc, auraDistance, true, -1, default, ModContent.BuffType<OiledBuff>());
                        float threshold = auraDistance;

                        Player localPlayer = Main.LocalPlayer;
                        float distance = localPlayer.Distance(npc.Center);
                        if (localPlayer.active && !localPlayer.dead && !localPlayer.ghost) //pull into arena
                        {
                            if (distance > threshold && distance < threshold * 4f)
                            {
                                if (distance > threshold * 2f)
                                {
                                    localPlayer.controlLeft = false;
                                    localPlayer.controlRight = false;
                                    localPlayer.controlUp = false;
                                    localPlayer.controlDown = false;
                                    localPlayer.controlUseItem = false;
                                    localPlayer.controlUseTile = false;
                                    localPlayer.controlJump = false;
                                    localPlayer.controlHook = false;
                                    if (localPlayer.grapCount > 0)
                                        localPlayer.RemoveAllGrapplingHooks();
                                    if (localPlayer.mount.Active)
                                        localPlayer.mount.Dismount(localPlayer);
                                    localPlayer.velocity.X = 0f;
                                    localPlayer.velocity.Y = -0.4f;
                                    localPlayer.FargoSouls().NoUsingItems = 2;
                                }

                                Vector2 movement = npc.Center - localPlayer.Center;
                                float difference = movement.Length() - threshold;
                                movement.Normalize();
                                movement *= difference < 30f ? difference : 30f;
                                localPlayer.position += movement;

                                for (int i = 0; i < 10; i++)
                                {
                                    int DustType = Main.rand.NextFromList(DustID.YellowTorch, DustID.PinkTorch, DustID.UltraBrightTorch);
                                    int d = Dust.NewDust(localPlayer.position, localPlayer.width, localPlayer.height, DustType, 0f, 0f, 0, default, 1.25f);
                                    Main.dust[d].noGravity = true;
                                    Main.dust[d].velocity *= 5f;
                                }
                            }
                        }
                    }

                    //2*pi * (# of full circles) / (seconds to finish rotation) / (ticks per sec)
                    float rotationInterval = 2f * (float)Math.PI * 1.2f / 4f / 60f;
                    if (WorldSavingSystem.MasochistModeReal)
                        rotationInterval *= 1.05f;

                    npc.ai[0]++; //base value is 4
                    switch (DeathrayState) //laser code idfk
                    {
                        case 0:
                            if (!npc.HasValidTarget)
                            {
                                npc.ai[0]--; //stop counting up if player is dead
                                if (spazmatism == null) //despawn REALLY fast
                                    npc.velocity.Y -= 0.5f;
                            }
                            if (npc.ai[0] > 604f)
                            {
                                npc.ai[0] = 4f;
                                if (npc.HasPlayerTarget)
                                {
                                    npc.rotation = npc.Center.X < Main.player[npc.target].Center.X ? 0 : (float)Math.PI;
                                    npc.rotation -= (float)Math.PI / 2;

                                    DeathrayState++;
                                    npc.ai[3] = -npc.rotation;
                                    if (--npc.ai[2] > 295f)
                                        npc.ai[2] = 295f;
                                    StoredDirectionToPlayer = Main.player[npc.target].Center.X - npc.Center.X < 0;

                                    if (FargoSoulsUtil.HostCheck)
                                        Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRing>(), 0, 0f, Main.myPlayer, npc.whoAmI, npc.type);

                                    SoundEngine.PlaySound(SoundID.ForceRoarPitched, npc.Center); //eoc roar
                                }

                                if (Main.netMode == NetmodeID.Server)
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                                NetSync(npc);
                            }
                            break;

                        case 1: //slowing down, beginning rotation
                            npc.velocity *= 1f - (npc.ai[0] - 4f) / 120f;
                            npc.localAI[1] = 0f;
                            //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                            npc.ai[3] -= (npc.ai[0] - 4f) / 120f * rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                            npc.rotation = -npc.ai[3];

                            if (npc.ai[0] == 35f)
                            {
                                if (FargoSoulsUtil.HostCheck)
                                {
                                    Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowLine>(), 0, 0f, Main.myPlayer, 9f, npc.whoAmI);
                                }
                            }

                            if (npc.ai[0] >= 155f) //FIRE LASER
                            {
                                if (FargoSoulsUtil.HostCheck)
                                {
                                    Vector2 speed = Vector2.UnitX.RotatedBy(npc.rotation);
                                    Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed, ModContent.ProjectileType<RetinazerDeathray>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 4f / 3), 0f, Main.myPlayer, 0f, npc.whoAmI);
                                }
                                DeathrayState++;
                                npc.ai[0] = 4f;

                                if (Main.netMode == NetmodeID.Server)
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                                NetSync(npc);
                            }
                            return false;

                        case 2: //spinning full speed
                            npc.velocity = Vector2.Zero;
                            npc.localAI[1] = 0f;
                            //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                            npc.ai[3] -= rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                            npc.rotation = -npc.ai[3];

                            if (npc.ai[0] >= 244f)
                            {
                                DeathrayState++;
                                npc.ai[0] = 4f;

                                if (Main.netMode == NetmodeID.Server)
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                                NetSync(npc);
                            }
                            else if (!npc.HasValidTarget) //end spin immediately if player dead
                            {
                                npc.TargetClosest(false);
                                if (!npc.HasValidTarget)
                                    npc.ai[0] = 244f;
                            }
                            return false;

                        case 3: //laser done, slowing down spin, moving again
                            npc.velocity *= (npc.ai[0] - 4f) / 60f;
                            npc.localAI[1] = 0f;
                            //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                            npc.ai[3] -= (1f - (npc.ai[0] - 4f) / 60f) * rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                            npc.rotation = -npc.ai[3];

                            if (npc.ai[0] >= 64f)
                            {
                                DeathrayState = 0;
                                npc.ai[0] = 4f;

                                if (Main.netMode == NetmodeID.Server)
                                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                                NetSync(npc);
                            }
                            return false;

                        default:
                            DeathrayState = 0;
                            npc.ai[0] = 4f;
                            npc.netUpdate = true;
                            NetSync(npc);
                            break;
                    }
                }
            }


            if (DeathrayState > 0)
                Resist = true;

            EModeUtils.DropSummon(npc, "MechEye", NPC.downedMechBoss2, ref DroppedSummon, Main.hardMode);

            return true;
        }
        /* OLD AI
        public override bool SafePreAI(NPC npc)
        {
            EModeGlobalNPC.retiBoss = npc.whoAmI;

            Resist = false;

            if (WorldSavingSystem.SwarmActive)
                return true;

            //have some dr during phase transition animation
            if (npc.ai[0] == 1 || npc.ai[0] == 2)
                Resist = true;

            NPC spazmatism = FargoSoulsUtil.NPCExists(EModeGlobalNPC.spazBoss, NPCID.Spazmatism);

            if (WorldSavingSystem.MasochistModeReal && spazmatism == null && npc.HasValidTarget && ++RespawnTimer > 600)
            {
                RespawnTimer = 0;
                if (FargoSoulsUtil.HostCheck)
                {
                    int n = FargoSoulsUtil.NewNPCEasy(npc.GetSource_FromThis(), npc.Center + new Vector2(Main.rand.NextFloat(-1000, 1000), Main.rand.NextFloat(-800, -600)), NPCID.Spazmatism, target: npc.target);
                    if (n != Main.maxNPCs)
                    {
                        Main.npc[n].life = Main.npc[n].lifeMax / 4;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, number: n);
                        FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsRevive", new Color(175, 75, 255), Main.npc[n].FullName);
                    }
                }
            }

            if (!ForcedPhase2OnSpawn) //start phase 2
            {
                ForcedPhase2OnSpawn = true;
                npc.ai[0] = 1f;
                npc.ai[1] = 0.0f;
                npc.ai[2] = 0.0f;
                npc.ai[3] = 0.0f;
                npc.netUpdate = true;
            }

            if (npc.life <= npc.lifeMax / 2 || npc.dontTakeDamage)
            {
                npc.dontTakeDamage = npc.life == 1 || !npc.HasValidTarget;
                if (npc.life != 1 && npc.HasValidTarget)
                    npc.dontTakeDamage = false;
                //become vulnerable again when both twins at low life
                if (npc.dontTakeDamage && npc.HasValidTarget && (spazmatism == null || spazmatism.life == 1))
                    npc.dontTakeDamage = false;
            }

            if (Main.dayTime && !Main.remixWorld)
            {
                if (npc.velocity.Y > 0)
                    npc.velocity.Y = 0;

                npc.velocity.Y -= 0.5f;
                npc.dontTakeDamage = true;

                if (spazmatism != null)
                {
                    if (npc.timeLeft < 60)
                        npc.timeLeft = 60;

                    if (spazmatism.timeLeft < 60)
                        spazmatism.timeLeft = 60;

                    npc.TargetClosest(false);
                    spazmatism.TargetClosest(false);
                    if (npc.Distance(Main.player[npc.target].Center) > 2000 && spazmatism.Distance(Main.player[spazmatism.target].Center) > 2000)
                    {
                        if (FargoSoulsUtil.HostCheck)
                        {
                            npc.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            spazmatism.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, EModeGlobalNPC.spazBoss);
                        }
                    }
                }

                return true;
            }

            if (npc.ai[0] < 4f) //going to phase 3
            {
                if (npc.life <= npc.lifeMax / 2)
                {
                    //npc.ai[0] = 4f;
                    npc.ai[0] = 604f; //initiate spin immediately
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                    if (FargoSoulsUtil.HostCheck)
                        Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRingHollow>(), 0, 0f, Main.myPlayer, 11, npc.whoAmI);
                }
            }
            else //in phase 3
            {

                if (WorldSavingSystem.MasochistModeReal && spazmatism == null && --MechElectricOrbTimer < 0) //when twin dead, begin shooting Electric Orbs
                {
                    MechElectricOrbTimer = 240;
                    if (FargoSoulsUtil.HostCheck && npc.HasPlayerTarget)
                    {
                        Vector2 distance = Main.player[npc.target].Center - npc.Center;
                        distance.Normalize();
                        distance *= 10f;
                        for (int i = 0; i < 12; i++)
                            Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, distance.RotatedBy(2 * Math.PI / 12 * i),
                                ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 0.8f), 0f, Main.myPlayer, ai2: MechElectricOrb.Yellow);
                    }
                }

                //dust code
                if (Main.rand.Next(4) < 3)
                {
                    int dustID = DeathrayState != 0 ? DustID.GemRuby : DustID.GemAmber;
                    int dust = Dust.NewDust(npc.position - new Vector2(2f, 2f), npc.width + 4, npc.height + 4, dustID, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 1.8f;
                    Main.dust[dust].velocity.Y -= 0.5f;
                    if (Main.rand.NextBool(4))
                    {
                        Main.dust[dust].noGravity = false;
                        Main.dust[dust].scale *= 0.5f;
                    }
                }

                if (npc.localAI[1] >= (npc.ai[1] == 0 ? 175 : 55)) //hijacking vanilla laser code
                {
                    npc.localAI[1] = 0;
                    Vector2 vel = npc.SafeDirectionTo(Main.player[npc.target].Center);
                    Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center + (npc.width - 24) * vel, vel, ModContent.ProjectileType<MechElectricOrbTwins>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer, npc.target, ai2: MechElectricOrb.Yellow);
                }

                if (DeathrayState == 0 || DeathrayState == 3) //not doing deathray, grow arena
                {
                    AuraRadiusCounter--;
                    if (AuraRadiusCounter < 0)
                        AuraRadiusCounter = 0;
                }
                else //doing deathray, shrink arena
                {
                    AuraRadiusCounter++;
                    if (AuraRadiusCounter > 180)
                        AuraRadiusCounter = 180;
                }

                float auraDistance = 2000 - 1200 * AuraRadiusCounter / 180f;
                if (WorldSavingSystem.MasochistModeReal)
                    auraDistance *= 0.75f;
                if (auraDistance < 2000 - 1)
                    EModeGlobalNPC.Aura(npc, auraDistance, true, DustID.Torch, default, ModContent.BuffType<OiledBuff>(), WorldSavingSystem.MasochistModeReal ? ModContent.BuffType<GodEaterBuff>() : BuffID.OnFire, BuffID.Burning);

                //2*pi * (# of full circles) / (seconds to finish rotation) / (ticks per sec)
                float rotationInterval = 2f * (float)Math.PI * 1.2f / 4f / 60f;
                if (WorldSavingSystem.MasochistModeReal)
                    rotationInterval *= 1.05f;

                npc.ai[0]++; //base value is 4
                switch (DeathrayState) //laser code idfk
                {
                    case 0:
                        if (!npc.HasValidTarget)
                        {
                            npc.ai[0]--; //stop counting up if player is dead
                            if (spazmatism == null) //despawn REALLY fast
                                npc.velocity.Y -= 0.5f;
                        }
                        if (npc.ai[0] > 604f)
                        {
                            npc.ai[0] = 4f;
                            if (npc.HasPlayerTarget)
                            {
                                npc.rotation = npc.Center.X < Main.player[npc.target].Center.X ? 0 : (float)Math.PI;
                                npc.rotation -= (float)Math.PI / 2;

                                DeathrayState++;
                                npc.ai[3] = -npc.rotation;
                                if (--npc.ai[2] > 295f)
                                    npc.ai[2] = 295f;
                                StoredDirectionToPlayer = Main.player[npc.target].Center.X - npc.Center.X < 0;

                                if (FargoSoulsUtil.HostCheck)
                                    Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRing>(), 0, 0f, Main.myPlayer, npc.whoAmI, npc.type);

                                SoundEngine.PlaySound(SoundID.ForceRoarPitched, npc.Center); //eoc roar
                            }

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            NetSync(npc);
                        }
                        break;

                    case 1: //slowing down, beginning rotation
                        npc.velocity *= 1f - (npc.ai[0] - 4f) / 120f;
                        npc.localAI[1] = 0f;
                        //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                        npc.ai[3] -= (npc.ai[0] - 4f) / 120f * rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                        npc.rotation = -npc.ai[3];

                        if (npc.ai[0] == 35f)
                        {
                            if (FargoSoulsUtil.HostCheck)
                            {
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowLine>(), 0, 0f, Main.myPlayer, 9f, npc.whoAmI);
                            }
                        }

                        if (npc.ai[0] >= 155f) //FIRE LASER
                        {
                            if (FargoSoulsUtil.HostCheck)
                            {
                                Vector2 speed = Vector2.UnitX.RotatedBy(npc.rotation);
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed, ModContent.ProjectileType<RetinazerDeathray>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 4f / 3), 0f, Main.myPlayer, 0f, npc.whoAmI);
                            }
                            DeathrayState++;
                            npc.ai[0] = 4f;

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            NetSync(npc);
                        }
                        return false;

                    case 2: //spinning full speed
                        npc.velocity = Vector2.Zero;
                        npc.localAI[1] = 0f;
                        //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                        npc.ai[3] -= rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                        npc.rotation = -npc.ai[3];

                        if (npc.ai[0] >= 244f)
                        {
                            DeathrayState++;
                            npc.ai[0] = 4f;

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            NetSync(npc);
                        }
                        else if (!npc.HasValidTarget) //end spin immediately if player dead
                        {
                            npc.TargetClosest(false);
                            if (!npc.HasValidTarget)
                                npc.ai[0] = 244f;
                        }
                        return false;

                    case 3: //laser done, slowing down spin, moving again
                        npc.velocity *= (npc.ai[0] - 4f) / 60f;
                        npc.localAI[1] = 0f;
                        //if (--npc.ai[2] > 295f) npc.ai[2] = 295f;
                        npc.ai[3] -= (1f - (npc.ai[0] - 4f) / 60f) * rotationInterval * (StoredDirectionToPlayer ? 1f : -1f);
                        npc.rotation = -npc.ai[3];

                        if (npc.ai[0] >= 64f)
                        {
                            DeathrayState = 0;
                            npc.ai[0] = 4f;

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            NetSync(npc);
                        }
                        return false;

                    default:
                        DeathrayState = 0;
                        npc.ai[0] = 4f;
                        npc.netUpdate = true;
                        NetSync(npc);
                        break;
                }
            }

            if (DeathrayState > 0)
                Resist = true;

            EModeUtils.DropSummon(npc, "MechEye", NPC.downedMechBoss2, ref DroppedSummon, Main.hardMode);

            return true;
        }
        */
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (Resist)
                modifiers.FinalDamage /= 2;

            base.ModifyIncomingHit(npc, ref modifiers);
        }

        public override Color? GetAlpha(NPC npc, Color drawColor)
        {
            if (npc.ai[0] < 4 || DeathrayState == 0f || DeathrayState == 3f)
                return base.GetAlpha(npc, drawColor);
            return new Color(255, drawColor.G / 2, drawColor.B / 2);
        }

        public override bool CheckDead(NPC npc)
        {
            if (WorldSavingSystem.SwarmActive || WorldSavingSystem.MasochistModeReal)
                return base.CheckDead(npc);

            if (FargoSoulsUtil.BossIsAlive(ref EModeGlobalNPC.spazBoss, NPCID.Spazmatism) && Main.npc[EModeGlobalNPC.spazBoss].life > 1) //spaz still active
            {
                npc.life = 1;
                npc.active = true;
                if (FargoSoulsUtil.HostCheck)
                    npc.netUpdate = true;

                if (!HasSaidEndure)
                {
                    HasSaidEndure = true;
                    FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsEndure", new Color(175, 75, 255), npc.FullName);
                }
                return false;
            }

            return base.CheckDead(npc);
        }

        public override void LoadSprites(NPC npc, bool recolor)
        {
            base.LoadSprites(npc, recolor);

            LoadNPCSprite(recolor, npc.type);
            LoadBossHeadSprite(recolor, 15);
            LoadBossHeadSprite(recolor, 20);
            LoadGoreRange(recolor, 143, 146);

            LoadSpecial(recolor, ref TextureAssets.Chain12, ref FargowiltasSouls.TextureBuffer.Chain12, "Chain12");
            LoadSpecial(recolor, ref TextureAssets.EyeLaser, ref FargowiltasSouls.TextureBuffer.EyeLaser, "Eye_Laser");
        }
    }

    public class Spazmatism : EModeNPCBehaviour
    {
        public override NPCMatcher CreateMatcher() => new NPCMatcher().MatchType(NPCID.Spazmatism);

        public int ProjectileTimer;
        public int FlameWheelSpreadTimer;
        public int FlameWheelCount;
        public int MechElectricOrbTimer;
        public int P3DashPhaseDelay;

        public bool Phase2;
        public bool HasSaidEndure;
        public bool Resist;
        public float RealRotation;
        public int RespawnTimer;

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            base.SendExtraAI(npc, bitWriter, binaryWriter);

            binaryWriter.Write7BitEncodedInt(ProjectileTimer);
            binaryWriter.Write7BitEncodedInt(FlameWheelSpreadTimer);
            binaryWriter.Write7BitEncodedInt(FlameWheelCount);
            binaryWriter.Write7BitEncodedInt(MechElectricOrbTimer);
            binaryWriter.Write7BitEncodedInt(P3DashPhaseDelay);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            base.ReceiveExtraAI(npc, bitReader, binaryReader);

            ProjectileTimer = binaryReader.Read7BitEncodedInt();
            FlameWheelSpreadTimer = binaryReader.Read7BitEncodedInt();
            FlameWheelCount = binaryReader.Read7BitEncodedInt();
            MechElectricOrbTimer = binaryReader.Read7BitEncodedInt();
            P3DashPhaseDelay = binaryReader.Read7BitEncodedInt();
        }

        public override void OnFirstTick(NPC npc)
        {
            base.OnFirstTick(npc);

            npc.buffImmune[BuffID.Suffocation] = true;
        }
        public override bool SafePreAI(NPC npc)
        {
            EModeGlobalNPC.spazBoss = npc.whoAmI;

            Resist = false;

            if (WorldSavingSystem.SwarmActive)
                return true;

            //have some dr during phase transition animation
            if (npc.ai[0] == 1 || npc.ai[0] == 2)
                Resist = true;

            NPC retinazer = FargoSoulsUtil.NPCExists(EModeGlobalNPC.retiBoss, NPCID.Retinazer);
            //FargoSoulsUtil.PrintAI(npc);

            if (WorldSavingSystem.MasochistModeReal && retinazer == null && npc.HasValidTarget && ++RespawnTimer > 600)
            {
                RespawnTimer = 0;
                if (FargoSoulsUtil.HostCheck)
                {
                    int n = FargoSoulsUtil.NewNPCEasy(npc.GetSource_FromThis(), npc.Center + new Vector2(Main.rand.NextFloat(-1000, 1000), Main.rand.NextFloat(-800, -600)), NPCID.Retinazer, target: npc.target);
                    if (n != Main.maxNPCs)
                    {
                        Main.npc[n].life = Main.npc[n].lifeMax / 4;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, number: n);
                        FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsRevive", new Color(175, 75, 255), Main.npc[n].FullName);
                    }
                }
            }

            float modifier = (float)npc.life / npc.lifeMax;
            if (WorldSavingSystem.MasochistModeReal)
                modifier *= modifier;

            
            if (!Phase2)
            {
                if (npc.GetLifePercent() < 0.66f || (retinazer != null && retinazer.GetLifePercent() < 0.66f))
                {
                    Phase2 = true;
                    npc.ai[0] = 1f;
                    npc.ai[1] = 0.0f;
                    npc.ai[2] = 0.0f;
                    npc.ai[3] = 0.0f;
                    npc.netUpdate = true;
                }
            }

            if (npc.life <= npc.lifeMax / 2 || npc.dontTakeDamage)
            {
                npc.dontTakeDamage = npc.life == 1 || !npc.HasValidTarget;
                if (npc.life != 1 && npc.HasValidTarget)
                    npc.dontTakeDamage = false;
                //become vulnerable again when both twins low
                if (npc.dontTakeDamage && npc.HasValidTarget && (retinazer == null || retinazer.life == 1))
                    npc.dontTakeDamage = false;
            }

            if (Main.dayTime && !Main.remixWorld)
            {
                if (npc.velocity.Y > 0)
                    npc.velocity.Y = 0;

                npc.velocity.Y -= 0.5f;
                npc.dontTakeDamage = true;

                if (retinazer != null)
                {
                    if (npc.timeLeft < 60)
                        npc.timeLeft = 60;

                    if (retinazer.timeLeft < 60)
                        retinazer.timeLeft = 60;

                    npc.TargetClosest(false);
                    retinazer.TargetClosest(false);
                    if (npc.Distance(Main.player[npc.target].Center) > 2000 && retinazer.Distance(Main.player[retinazer.target].Center) > 2000)
                    {
                        if (FargoSoulsUtil.HostCheck)
                        {
                            npc.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            retinazer.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, EModeGlobalNPC.retiBoss);
                        }
                    }
                }

                return true;
            }

            const int P3DashDelayLength = 75;

            if (!Phase2) // phase 1
            {
                ref float ai_State = ref npc.ai[1];
                if (!npc.HasPlayerTarget)
                    return true;
                Player player = Main.player[npc.target];

                switch (ai_State)
                {
                    case 0: //normal fireball state
                        {

                            // reworked p1 fireballs
                            float delay = 58f;
                            if (npc.ai[3] >= delay)
                            {
                                npc.ai[3] = 0f;
                                Vector2 shootPos = new Vector2(npc.position.X + (float)npc.width * 0.5f, npc.position.Y + (float)npc.height * 0.5f);
                                float targetX = player.Center.X - shootPos.X;
                                float targetY = player.Center.Y - shootPos.Y;
                                if (FargoSoulsUtil.HostCheck)
                                {
                                    float vel = 14f;
                                    int attackDamage_ForProjectiles6 = npc.GetAttackDamage_ForProjectiles(25f, 22f);
                                    float angle = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
                                    angle = vel / angle;
                                    targetX *= angle;
                                    targetY *= angle;
                                    targetX += (float)Main.rand.Next(-40, 41) * 0.05f;
                                    targetY += (float)Main.rand.Next(-40, 41) * 0.05f;
                                    shootPos.X += targetX * 4f;
                                    shootPos.Y += targetY * 4f;
                                    int num473 = Projectile.NewProjectile(npc.GetSource_FromThis(), shootPos.X, shootPos.Y, targetX / 10f, targetY / 10f, ModContent.ProjectileType<MechElectricOrbSpaz>(), 
                                        attackDamage_ForProjectiles6, 0f, Main.myPlayer, ai0: npc.target, ai2: MechElectricOrb.Green);

                                }
                            }
                        }
                        break;
                    case 1: // dash frame
                        {

                        }
                        break;
                    case 2: // mid dash
                        {
                            if (npc.ai[2] == 1f)
                                npc.velocity += npc.velocity.SafeNormalize(Vector2.UnitX) * npc.ai[3] * 3f;
                        }
                        break;
                }
            }
            else
            {
                if (npc.ai[1] == 0f) // not dashing
                {
                    if (retinazer != null && (retinazer.ai[0] < 4f || retinazer.GetGlobalNPC<Retinazer>().DeathrayState == 0
                        || retinazer.GetGlobalNPC<Retinazer>().DeathrayState == 3)) //reti is in normal AI
                    {
                        npc.ai[1] = 1; //switch to dashing
                        npc.ai[2] = 0;
                        npc.ai[3] = 0;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        NetSync(npc);
                        return false;
                    }
                }
                else //dashing
                {
                    if (FlameWheelSpreadTimer > 0) //cooldown before attacking again
                    {
                        P3DashPhaseDelay = Math.Min(FlameWheelSpreadTimer, 75);
                        FlameWheelSpreadTimer = 0;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        NetSync(npc);
                    }

                    if (P3DashPhaseDelay > 0)
                    {
                        P3DashPhaseDelay--;
                        if (!TryWatchHarmlessly(npc))
                            return false;
                    }
                    if (npc.ai[2] > 50)
                    {
                        npc.ai[2] -= modifier;
                    }
                    else
                    {
                        if (npc.HasValidTarget && ++ProjectileTimer > 3) //cursed flamethrower when dashing
                        {
                            float dashTime = 50f;
                            float extension = MathF.Sin(MathF.PI * npc.ai[2] / dashTime);
                            if (extension < 0)
                                extension = 0;
                            ProjectileTimer = 0;
                            if (FargoSoulsUtil.HostCheck)
                            {
                                float speed = extension * 0.8f;
                                float rotationVariance = 9f * extension * 0.75f;
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed * npc.velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-rotationVariance, rotationVariance))), ProjectileID.EyeFire, FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer);
                            }
                        }
                    }
                }

                if (npc.ai[0] < 4f)
                {
                    if (npc.life <= npc.lifeMax / 2 || retinazer.life <= retinazer.lifeMax / 2) //going to phase 3
                    {
                        npc.ai[0] = 4f;
                        npc.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                        if (!WorldSavingSystem.MasochistModeReal)
                            P3DashPhaseDelay = P3DashDelayLength;

                        if (FargoSoulsUtil.HostCheck)
                            Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRing>(), 0, 0f, Main.myPlayer, npc.whoAmI, NPCID.MoonLordCore);

                        int index = npc.FindBuffIndex(BuffID.CursedInferno);
                        if (index != -1)
                            npc.DelBuff(index); //remove cursed inferno debuff if i have it

                        npc.buffImmune[BuffID.CursedInferno] = true;
                        npc.buffImmune[BuffID.OnFire] = true;
                        npc.buffImmune[BuffID.OnFire3] = true;
                        npc.buffImmune[BuffID.ShadowFlame] = true;
                        npc.buffImmune[BuffID.Frostburn] = true;
                        npc.buffImmune[BuffID.Frostburn2] = true;
                    }

                    //reti is doing the spin
                    if (retinazer != null && retinazer.ai[0] >= 4f && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 0 && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 3)
                    {
                        if (!WorldSavingSystem.MasochistModeReal)
                        {
                            npc.velocity *= 0.98f;
                            if (!TryWatchHarmlessly(npc))
                                return false;
                        }
                    }
                }
                else //in phase 3
                {
                    //npc.position += npc.velocity / 10f;

                    //dust code
                    if (npc.ai[1] == 0f && Main.rand.Next(4) < 3)
                    {
                        int dust = Dust.NewDust(npc.position - new Vector2(2f, 2f), npc.width + 4, npc.height + 4, DustID.GemEmerald, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f, 100, default, 3.5f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity *= 1.8f;
                        Main.dust[dust].velocity.Y -= 0.5f;
                        if (Main.rand.NextBool(4))
                        {
                            Main.dust[dust].noGravity = false;
                            Main.dust[dust].scale *= 0.5f;
                        }
                    }

                    if (npc.ai[1] == 0f) //not dashing
                    {
                        Resist = true;
                        if (npc.HasValidTarget && retinazer != null)
                        {
                            Vector2 target = retinazer.Center + retinazer.SafeDirectionTo(npc.Center) * 100;
                            npc.velocity = (target - npc.Center) / 60;

                            float rotationInterval = 2f * (float)Math.PI * 1.2f / 4f / 60f * 0.65f;
                            rotationInterval *= retinazer.GetGlobalNPC<Retinazer>().StoredDirectionToPlayer ? 1f : -1f;
                            //if (WorldSavingSystem.MasochistModeReal)
                            rotationInterval *= -1f;

                            npc.rotation += rotationInterval * ProjectileTimer / 20f;
                            RealRotation += rotationInterval;

                            if (FlameWheelSpreadTimer < 0)
                                FlameWheelSpreadTimer = 0;

                            if (FlameWheelCount == 0) //i can't be bothered to figure out the formula for this rn
                            {
                                FlameWheelCount = 2;
                                if (modifier < 0.5f / 4 * 3)
                                    FlameWheelCount = 3;
                                if (modifier < 0.5f / 4 * 2)
                                    FlameWheelCount = 4;
                                if (modifier < 0.5f / 4 * 1 || WorldSavingSystem.MasochistModeReal)
                                    FlameWheelCount = 5;

                                ProjectileTimer = 0;
                            }

                            if (++FlameWheelSpreadTimer < 30) //snap to reti, don't do contact damage
                            {
                                npc.rotation = npc.SafeDirectionTo(retinazer.Center).ToRotation() - (float)Math.PI / 2;
                                RealRotation = npc.rotation;
                            }
                            else if (++ProjectileTimer % 30 == 0) //rings of stars
                            {
                                if (FargoSoulsUtil.HostCheck)
                                {
                                    float speed = 12f * Math.Min((FlameWheelSpreadTimer - 30) / 120f, 1f); //fan out gradually
                                    int timeLeft = (int)(speed / 12f * 90f);
                                    float baseRotation = RealRotation + (float)Math.PI / 2;
                                    if (timeLeft > 5)
                                    {
                                        for (int i = 0; i < FlameWheelCount; i++)
                                        {
                                            int p = Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed * (baseRotation + MathHelper.TwoPi / FlameWheelCount * i).ToRotationVector2(),
                                                ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer, ai2: MechElectricOrb.Green);
                                            if (p != Main.maxProjectiles)
                                                Main.projectile[p].timeLeft = timeLeft;
                                        }
                                    }
                                }
                            }

                            /*if (++Counter0 > 40)
                            {
                                Counter0 = 0;
                                if (FargoSoulsUtil.HostCheck && npc.HasPlayerTarget) //vanilla spaz p1 shoot fireball code
                                {
                                    Vector2 Speed = Main.player[npc.target].Center - npc.Center;
                                    Speed.Normalize();
                                    int Damage;
                                    if (Main.expertMode)
                                    {
                                        Speed *= 14f;
                                        Damage = 22;
                                    }
                                    else
                                    {
                                        Speed *= 12f;
                                        Damage = 25;
                                    }
                                    Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center + Speed * 4f, Speed, ProjectileID.CursedFlameHostile, Damage, 0f, Main.myPlayer);
                                }
                            }*/

                            return false;
                        }
                    }
                    else //dashing
                    {
                        if (retinazer != null && retinazer.ai[0] >= 4f && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 0 && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 3) //reti is doing the spin
                        {
                            npc.ai[1] = 0; //switch to not dashing

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            NetSync(npc);
                            return false;
                        }

                        FlameWheelCount = 0;
                    }

                    if (WorldSavingSystem.MasochistModeReal && retinazer == null && --MechElectricOrbTimer < 0) //when twin dead, begin shooting Electric Orbs
                    {
                        MechElectricOrbTimer = 150;
                        if (FargoSoulsUtil.HostCheck && npc.HasPlayerTarget)
                        {
                            Vector2 distance = Main.player[npc.target].Center - npc.Center;
                            distance.Normalize();
                            distance *= 14f;
                            for (int i = 0; i < 8; i++)
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, distance.RotatedBy(2 * Math.PI / 8 * i),
                                    ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 0.8f), 0f, Main.myPlayer, ai2: MechElectricOrb.Green);
                        }
                    }
                }
            }
            return true;
        }
        /* OLD AI
        public override bool SafePreAI(NPC npc)
        {
            EModeGlobalNPC.spazBoss = npc.whoAmI;

            Resist = false;

            if (WorldSavingSystem.SwarmActive)
                return true;

            //have some dr during phase transition animation
            if (npc.ai[0] == 1 || npc.ai[0] == 2)
                Resist = true;

            NPC retinazer = FargoSoulsUtil.NPCExists(EModeGlobalNPC.retiBoss, NPCID.Retinazer);

            if (WorldSavingSystem.MasochistModeReal && retinazer == null && npc.HasValidTarget && ++RespawnTimer > 600)
            {
                RespawnTimer = 0;
                if (FargoSoulsUtil.HostCheck)
                {
                    int n = FargoSoulsUtil.NewNPCEasy(npc.GetSource_FromThis(), npc.Center + new Vector2(Main.rand.NextFloat(-1000, 1000), Main.rand.NextFloat(-800, -600)), NPCID.Retinazer, target: npc.target);
                    if (n != Main.maxNPCs)
                    {
                        Main.npc[n].life = Main.npc[n].lifeMax / 4;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, number: n);
                        FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsRevive", new Color(175, 75, 255), Main.npc[n].FullName);
                    }
                }
            }

            float modifier = (float)npc.life / npc.lifeMax;
            if (WorldSavingSystem.MasochistModeReal)
                modifier *= modifier;

            if (!ForcedPhase2OnSpawn) //spawn in phase 2
            {
                ForcedPhase2OnSpawn = true;
                npc.ai[0] = 1f;
                npc.ai[1] = 0.0f;
                npc.ai[2] = 0.0f;
                npc.ai[3] = 0.0f;
                npc.netUpdate = true;
            }

            if (npc.life <= npc.lifeMax / 2 || npc.dontTakeDamage)
            {
                npc.dontTakeDamage = npc.life == 1 || !npc.HasValidTarget;
                if (npc.life != 1 && npc.HasValidTarget)
                    npc.dontTakeDamage = false;
                //become vulnerable again when both twins low
                if (npc.dontTakeDamage && npc.HasValidTarget && (retinazer == null || retinazer.life == 1))
                    npc.dontTakeDamage = false;
            }

            if (Main.dayTime && !Main.remixWorld)
            {
                if (npc.velocity.Y > 0)
                    npc.velocity.Y = 0;

                npc.velocity.Y -= 0.5f;
                npc.dontTakeDamage = true;

                if (retinazer != null)
                {
                    if (npc.timeLeft < 60)
                        npc.timeLeft = 60;

                    if (retinazer.timeLeft < 60)
                        retinazer.timeLeft = 60;

                    npc.TargetClosest(false);
                    retinazer.TargetClosest(false);
                    if (npc.Distance(Main.player[npc.target].Center) > 2000 && retinazer.Distance(Main.player[retinazer.target].Center) > 2000)
                    {
                        if (FargoSoulsUtil.HostCheck)
                        {
                            npc.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                            retinazer.active = false;
                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, EModeGlobalNPC.retiBoss);
                        }
                    }
                }

                return true;
            }

            const int P3DashDelayLength = 75;

            if (npc.ai[0] < 4f)
            {
                if (npc.life <= npc.lifeMax / 2) //going to phase 3
                {
                    npc.ai[0] = 4f;
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                    if (!WorldSavingSystem.MasochistModeReal)
                        P3DashPhaseDelay = P3DashDelayLength;

                    if (FargoSoulsUtil.HostCheck)
                        Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GlowRing>(), 0, 0f, Main.myPlayer, npc.whoAmI, NPCID.MoonLordCore);

                    int index = npc.FindBuffIndex(BuffID.CursedInferno);
                    if (index != -1)
                        npc.DelBuff(index); //remove cursed inferno debuff if i have it

                    npc.buffImmune[BuffID.CursedInferno] = true;
                    npc.buffImmune[BuffID.OnFire] = true;
                    npc.buffImmune[BuffID.OnFire3] = true;
                    npc.buffImmune[BuffID.ShadowFlame] = true;
                    npc.buffImmune[BuffID.Frostburn] = true;
                    npc.buffImmune[BuffID.Frostburn2] = true;
                }

                //reti is doing the spin
                if (retinazer != null && retinazer.ai[0] >= 4f && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 0 && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 3)
                {
                    if (!WorldSavingSystem.MasochistModeReal)
                    {
                        npc.velocity *= 0.98f;
                        if (!TryWatchHarmlessly(npc))
                            return false;
                    }
                }
            }
            else //in phase 3
            {
                //npc.position += npc.velocity / 10f;

                //dust code
                if (Main.rand.Next(4) < 3)
                {
                    int dust = Dust.NewDust(npc.position - new Vector2(2f, 2f), npc.width + 4, npc.height + 4, DustID.GemEmerald, npc.velocity.X * 0.4f, npc.velocity.Y * 0.4f, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 1.8f;
                    Main.dust[dust].velocity.Y -= 0.5f;
                    if (Main.rand.NextBool(4))
                    {
                        Main.dust[dust].noGravity = false;
                        Main.dust[dust].scale *= 0.5f;
                    }
                }

                if (npc.ai[1] == 0f) //not dashing
                {
                    Resist = true;

                    if (retinazer != null && (retinazer.ai[0] < 4f || retinazer.GetGlobalNPC<Retinazer>().DeathrayState == 0
                        || retinazer.GetGlobalNPC<Retinazer>().DeathrayState == 3)) //reti is in normal AI
                    {
                        npc.ai[1] = 1; //switch to dashing
                        npc.ai[2] = 0;
                        npc.ai[3] = 0;

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        NetSync(npc);
                        return false;
                    }

                    if (npc.HasValidTarget && retinazer != null)
                    {
                        Vector2 target = retinazer.Center + retinazer.SafeDirectionTo(npc.Center) * 100;
                        npc.velocity = (target - npc.Center) / 60;

                        float rotationInterval = 2f * (float)Math.PI * 1.2f / 4f / 60f * 0.65f;
                        rotationInterval *= retinazer.GetGlobalNPC<Retinazer>().StoredDirectionToPlayer ? 1f : -1f;
                        if (WorldSavingSystem.MasochistModeReal)
                            rotationInterval *= -1f;

                        npc.rotation += rotationInterval * ProjectileTimer / 20f;
                        RealRotation += rotationInterval;

                        if (FlameWheelSpreadTimer < 0)
                            FlameWheelSpreadTimer = 0;

                        if (FlameWheelCount == 0) //i can't be bothered to figure out the formula for this rn
                        {
                            FlameWheelCount = 2;
                            if (modifier < 0.5f / 4 * 3)
                                FlameWheelCount = 3;
                            if (modifier < 0.5f / 4 * 2)
                                FlameWheelCount = 4;
                            if (modifier < 0.5f / 4 * 1 || WorldSavingSystem.MasochistModeReal)
                                FlameWheelCount = 5;

                            ProjectileTimer = 0;
                        }

                        if (++FlameWheelSpreadTimer < 30) //snap to reti, don't do contact damage
                        {
                            npc.rotation = npc.SafeDirectionTo(retinazer.Center).ToRotation() - (float)Math.PI / 2;
                            RealRotation = npc.rotation;
                        }
                        else if (++ProjectileTimer % 15 == 0) //rings of stars
                        {
                            if (FargoSoulsUtil.HostCheck)
                            {
                                float speed = 12f * Math.Min((FlameWheelSpreadTimer - 30) / 120f, 1f); //fan out gradually
                                int timeLeft = (int)(speed / 12f * 90f);
                                float baseRotation = RealRotation + (float)Math.PI / 2;
                                if (timeLeft > 5)
                                {
                                    for (int i = 0; i < FlameWheelCount; i++)
                                    {
                                        int p = Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed * (baseRotation + MathHelper.TwoPi / FlameWheelCount * i).ToRotationVector2(),
                                            ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer, ai2: MechElectricOrb.Green);
                                        if (p != Main.maxProjectiles)
                                            Main.projectile[p].timeLeft = timeLeft;
                                    }
                                }
                            }
                        }

                        return false;
                    }
                }
                else //dashing
                {
                    if (retinazer != null && retinazer.ai[0] >= 4f && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 0 && retinazer.GetGlobalNPC<Retinazer>().DeathrayState != 3) //reti is doing the spin
                    {
                        npc.ai[1] = 0; //switch to not dashing

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        NetSync(npc);
                        return false;
                    }

                    FlameWheelCount = 0;

                    if (FlameWheelSpreadTimer > 0) //cooldown before attacking again
                    {
                        P3DashPhaseDelay = Math.Min(FlameWheelSpreadTimer, 75);
                        FlameWheelSpreadTimer = 0;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                        NetSync(npc);
                    }

                    if (P3DashPhaseDelay > 0)
                    {
                        P3DashPhaseDelay--;
                        if (!TryWatchHarmlessly(npc))
                            return false;
                    }

                    if (npc.ai[2] > 50)
                    {
                        npc.ai[2] -= modifier;
                    }
                    else
                    {
                        if (npc.HasValidTarget && ++ProjectileTimer > 3) //cursed flamethrower when dashing
                        {
                            ProjectileTimer = 0;
                            if (FargoSoulsUtil.HostCheck)
                            {
                                float speed = (1f - modifier) * 0.8f;
                                float rotationVariance = 9f * modifier * 2;
                                Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, speed * npc.velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-rotationVariance, rotationVariance))), ProjectileID.EyeFire, FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage), 0f, Main.myPlayer);
                            }
                        }
                    }
                }

                if (WorldSavingSystem.MasochistModeReal && retinazer == null && --MechElectricOrbTimer < 0) //when twin dead, begin shooting Electric Orbs
                {
                    MechElectricOrbTimer = 150;
                    if (FargoSoulsUtil.HostCheck && npc.HasPlayerTarget)
                    {
                        Vector2 distance = Main.player[npc.target].Center - npc.Center;
                        distance.Normalize();
                        distance *= 14f;
                        for (int i = 0; i < 8; i++)
                            Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, distance.RotatedBy(2 * Math.PI / 8 * i),
                                ModContent.ProjectileType<MechElectricOrb>(), FargoSoulsUtil.ScaledProjectileDamage(npc.defDamage, 0.8f), 0f, Main.myPlayer, ai2: MechElectricOrb.Green);
                    }
                }
            }

            return true;
        }
        */
        private bool TryWatchHarmlessly(NPC npc)
        {
            Resist = true;

            if (npc.HasValidTarget)
            {
                const float PI = (float)Math.PI;
                if (npc.rotation > PI)
                    npc.rotation -= 2 * PI;
                if (npc.rotation < -PI)
                    npc.rotation += 2 * PI;

                float targetRotation = npc.SafeDirectionTo(Main.player[npc.target].Center).ToRotation() - PI / 2;
                if (targetRotation > PI)
                    targetRotation -= 2 * PI;
                if (targetRotation < -PI)
                    targetRotation += 2 * PI;
                npc.rotation = MathHelper.Lerp(npc.rotation, targetRotation, 0.07f);

                return false;
            }
            return true;
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (Resist)
                modifiers.FinalDamage /= 2;

            base.ModifyIncomingHit(npc, ref modifiers);
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int CooldownSlot)
        {
            return !(npc.ai[1] == 0f && FlameWheelSpreadTimer < 30);
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            base.OnHitPlayer(npc, target, hurtInfo);

            if (npc.ai[0] >= 4f)
                target.AddBuff(BuffID.CursedInferno, 300);
        }

        public override Color? GetAlpha(NPC npc, Color drawColor)
        {
            if (npc.ai[0] < 4 || npc.ai[1] != 0f)
                return base.GetAlpha(npc, drawColor);
            return new Color(drawColor.R / 2, 255, drawColor.B / 2);
        }

        public override bool CheckDead(NPC npc)
        {
            if (WorldSavingSystem.SwarmActive || WorldSavingSystem.MasochistModeReal)
                return base.CheckDead(npc);

            if (FargoSoulsUtil.BossIsAlive(ref EModeGlobalNPC.retiBoss, NPCID.Retinazer) && Main.npc[EModeGlobalNPC.retiBoss].life > 1) //reti still active
            {
                npc.life = 1;
                npc.active = true;
                if (FargoSoulsUtil.HostCheck)
                    npc.netUpdate = true;

                if (!HasSaidEndure)
                {
                    HasSaidEndure = true;
                    FargoSoulsUtil.PrintLocalization($"Mods.{Mod.Name}.NPCs.EMode.TwinsEndure", new Color(175, 75, 255), npc.FullName);
                }
                return false;
            }

            return base.CheckDead(npc);
        }

        public override void LoadSprites(NPC npc, bool recolor)
        {
            base.LoadSprites(npc, recolor);

            LoadNPCSprite(recolor, npc.type);
            LoadBossHeadSprite(recolor, 16);
            LoadBossHeadSprite(recolor, 21);
            LoadGoreRange(recolor, 143, 146);

            LoadSpecial(recolor, ref TextureAssets.Chain12, ref FargowiltasSouls.TextureBuffer.Chain12, "Chain12");
        }
    }
}
