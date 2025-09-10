using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CalamityMod;
using CalamityMod.Buffs.StatBuffs;
using CalamityMod.MainMenu;
using CalamityMod.UI;
using InfernumMode.Assets.Fonts;
using InfernumMode.Assets.Effects;
using InfernumMode.Assets.ExtraTextures;
using InfernumMode.Assets.Sounds;
using InfernumMode.Common.DataStructures;
using InfernumMode.Core;
using InfernumMode.Content.MainMenu;
using InfernumMode.Content.BossIntroScreens;
using InfernumMode.Content.BossIntroScreens.InfernumScreens;
using InfernumMode.Core.ModCalls.InfernumCalls.IntroScreenModCalls;
using ReLogic.Content;
using ReLogic.Graphics;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.Chat;
using Terraria.UI.Chat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;

namespace InfernumKoreanLocalization;

public class InfernumKoreanLocalization : ModSystem
{
    public DynamicSpriteFont bossIntroScreensFont = null;
    public DynamicSpriteFont profanedTextFont = ModContent.Request<DynamicSpriteFont>($"InfernumKoreanLocalization/Assets/Fonts/ProfanedTextK", AssetRequestMode.ImmediateLoad).Value;

    public static InfernumKoreanLocalizationConfig.FontList? _lastAppliedFont = null;

    private ILHook introHook;

    public override void PostSetupContent()
    {
        if (Main.netMode == NetmodeID.Server) return;
        if (Main.dedServ) return; 
        
        var config = ModContent.GetInstance<InfernumKoreanLocalization.InfernumKoreanLocalizationConfig>();

        var getter = typeof(BaseIntroScreen).GetProperty("TextToDisplay", BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
        if (getter != null)
        {
            introHook = new ILHook(getter, il =>
            {
                var getLocalizedMethod = typeof(BaseIntroScreen).GetMethod("GetLocalizedText", BindingFlags.NonPublic | BindingFlags.Instance);

                var c = new ILCursor(il);
                if (c.TryGotoNext(MoveType.Before, x => x.MatchRet()))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldarg_0);

                    c.EmitDelegate<Func<BaseIntroScreen, BaseIntroScreen, LocalizedText, LocalizedText>>((selfForMethod, selfForCheck, original) =>
                    {
                        if (original != null && original.Key != null &&
                            original.Key.StartsWith("Mods.InfernumMode.IntroScreen.ModCallIntroScreen"))
                            return original;
                        
                        if (config.BossIntroScreensLocalization)
                            return original;
                        else
                        {
                            return (LocalizedText)getLocalizedMethod.Invoke(selfForMethod, new object[] { "TextToDisplay.EN" });
                        }
                    });
                }
            });
        }
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
        ChangeIntroFontPostSetted(config.ChangeIntroFont);
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.Server) return;
        if (Main.dedServ) return;

        var introScreensField = typeof(IntroScreenManager).GetField("IntroScreens", BindingFlags.NonPublic | BindingFlags.Static);
        if (introScreensField == null) return;

        var screens = introScreensField.GetValue(null) as List<BaseIntroScreen>;
        if (screens == null) return;

        var config = ModContent.GetInstance<InfernumKoreanLocalization.InfernumKoreanLocalizationConfig>();

        if (_lastAppliedFont != config.ChangeIntroFont)
        {
            _lastAppliedFont = config.ChangeIntroFont;
            ChangeIntroFontPostSetted(config.ChangeIntroFont);
        }

        foreach (var screen in screens)
        {
            try
            {
                string key = config.BossIntroScreensLocalization ? "TextToDisplay" : "TextToDisplay.EN";
                var getLocalizedMethod = typeof(BaseIntroScreen).GetMethod("GetLocalizedText", BindingFlags.NonPublic | BindingFlags.Instance);
                if (getLocalizedMethod == null)
                    continue;

                var text = (LocalizedText)getLocalizedMethod.Invoke(screen, new object[] { key });
                if (text == null || text.Key == null)
                    continue;

                if (text.Key.StartsWith("Mods.InfernumMode.IntroScreen.ModCallIntroScreen"))
                    continue;

                if (text.Value == text.Key)
                    continue;

                if (text.Key.StartsWith("Mods.InfernumMode"))
                {
                    screen.CachedText = text.Value;
                    // Main.NewText($"Key={text.Key}, Value={text.Value}");
                }
            }
            catch
            {
            }
        }
    }

    public void ChangeIntroFontPostSetted(InfernumKoreanLocalizationConfig.FontList fontChoice)
    {
        var config = ModContent.GetInstance<InfernumKoreanLocalizationConfig>();
        string fontNamePrefix = config.ChangeIntroFont.ToString();

        if (config.ChangeIntroFont == InfernumKoreanLocalizationConfig.FontList.vanilla)
        {
            bossIntroScreensFont = Terraria.GameContent.FontAssets.DeathText.Value; //Vanilla Font
        }
        else
        {
            bossIntroScreensFont = ModContent.Request<DynamicSpriteFont>($"InfernumKoreanLocalization/Assets/Fonts/BossIntroScreensFontKList/{fontNamePrefix}", AssetRequestMode.ImmediateLoad).Value;
        }

        var profanedTextFontProperty = typeof(InfernumFontRegistry).GetProperty("ProfanedTextFont", BindingFlags.Static | BindingFlags.Public);

        var bossIntroScreensFontProperty = typeof(InfernumFontRegistry).GetProperty("BossIntroScreensFont", BindingFlags.Static | BindingFlags.Public);

        if (bossIntroScreensFontProperty != null && bossIntroScreensFont != null)
        {
            var newBossIntroScreensFont = new LocalizedSpriteFont(bossIntroScreensFont, GameCulture.CultureName.English).WithLanguage(GameCulture.CultureName.English, bossIntroScreensFont);
            bossIntroScreensFontProperty.SetValue(null, newBossIntroScreensFont);
        }

        if (profanedTextFontProperty != null && profanedTextFont != null)
        {
            var newProfanedTextFont = new LocalizedSpriteFont(profanedTextFont, GameCulture.CultureName.English).WithLanguage(GameCulture.CultureName.English, profanedTextFont);
            profanedTextFontProperty.SetValue(null, newProfanedTextFont);
        }
    }

    public override void Unload()
    {
        if (Main.netMode == NetmodeID.Server) return;
        if (Main.dedServ) return;

        introHook?.Dispose();
        introHook = null;
    }

    public class InfernumKoreanLocalizationConfig : ModConfig
    {
        public enum FontList
        {
            neurimbo,
            climateCrisis,
		    vanilla
        }

        public static InfernumKoreanLocalizationConfig Instance => ModContent.GetInstance<InfernumKoreanLocalizationConfig>();
        
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [DefaultValue(true)]
        public bool BossIntroScreensLocalization { get; set; }

        [DefaultValue(FontList.neurimbo)]
        public FontList ChangeIntroFont { get; set; }
    }
}
