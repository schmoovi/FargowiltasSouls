﻿using FargowiltasSouls.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.UI;

namespace FargowiltasSouls.Content.UI.Elements
{
    public class CooldownBarManager : UIState
    {
        public static List<UICooldownBar> uiCooldownBars = [];
        public static CooldownBarManager Instance;
        //public static List<UICooldownBar> CooldownBars;
        public const int BarWidth = 100;
        public const int BarHeight = 20;
        public const int SpaceBetweenBars = 20;
        public static void Activate(string nameKey, Texture2D itemTexture, Color fillColor, Func<float> fillRatio, bool displayAtFull = false, int fadeDelay = 0)
        {
            if (!SoulConfig.Instance.CooldownBars)
                return;
            if (Instance.Children.Any(b => b is UICooldownBar cdBar && cdBar.Active && cdBar.NameKey == nameKey))
                return;
            float x = SoulConfig.Instance.CooldownBarsX;
            float y = SoulConfig.Instance.CooldownBarsY;
            int i = Instance.Children.Count();

            // If there's a free unused bar, transform it into the new bar
            if (Instance.Children.FirstOrDefault(b => b is UICooldownBar cdBar && !cdBar.Active) is UICooldownBar freeBar)
            {
                freeBar.Opacity = 0f;
                freeBar.Active = true;

                freeBar.NameKey = nameKey;
                freeBar.ItemTexture = itemTexture;
                freeBar.FillColor = fillColor;
                freeBar.FillRatio = fillRatio;
                freeBar.DisplayAtFull = displayAtFull;
                freeBar.FadeDelay = fadeDelay;
            }
            else
            {
                UICooldownBar newBar = new(nameKey, FargoUIManager.CooldownBarTexture.Value, FargoUIManager.CooldownBarFillTexture.Value, itemTexture, fillColor, fillRatio, displayAtFull, fadeDelay);
                //int nX = i / 3;
                //int nY = i % 3;
                int nY = i;
                //newBar.Left.Set(x + nX * (SpaceBetweenBars + BarWidth), 0);
                newBar.Left.Set(x, 0);
                newBar.Top.Set(y + nY * (SpaceBetweenBars + BarHeight), 0);
                Instance.Append(newBar);
            }
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
        public override void OnActivate()
        {
            Instance = this;
            base.OnActivate();
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!SoulConfig.Instance.CooldownBars)
                return;
            base.Draw(spriteBatch);
            /*
            foreach (UICooldownBar cooldownBar in CooldownBars)
            {
                cooldownBar.Draw(spriteBatch);
            }*/
        }
    }
}