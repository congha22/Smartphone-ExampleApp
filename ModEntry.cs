// =============================================================================
// ModEntry.cs  –  Mod Entry Point
// =============================================================================
// This is the main entry point of the Smartphone-ExampleApps mod.
//
// It demonstrates:
//  1. How to fetch the Smartphone API via SMAPI's mod registry.
//  2. How to load themed icon textures (1x1 and 4x2 variants).
//  3. How to register a Portrait app and a Landscape app.
//  4. How to declare supported widget sizes (1x1, 2x2, 4x2).
//  5. How to implement the onDrawWidget callback for custom widget drawing.
//  6. How to keep the 4x2 live animation updated every game tick.
// =============================================================================

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmartphoneExampleApps.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SmartphoneExampleApps
{
    // =========================================================================
    // IContactActionCardButton implementation
    // =========================================================================
    // The Smartphone API uses an interface for contact card buttons so any mod
    // can implement it. We define a simple concrete class here.

    /// <summary>
    /// Concrete implementation of a contact action card button.
    /// Provide Text, colours, and an OnClick callback, then pass a list of
    /// these to api.RegisterContactActionCard(...).
    /// </summary>
    public class ExampleContactButton : IContactActionCardButton
    {
        public string Text { get; set; } = string.Empty;
        public Color  BackgroundColor { get; set; } = Color.White;
        public Color  TextColor       { get; set; } = Color.Black;
        public Action<string>? OnClick { get; set; }
    }

    internal sealed class ModEntry : Mod
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        /// <summary>The unique ID of the Smartphone framework mod.</summary>
        private const string SmartphoneModId = "d5a1lamdtd.Smartphone";

        // App IDs – these combine with your UniqueID to form the composite key
        // that the framework uses for theme look-ups:  "YourMod.UniqueID::appId"
        private const string PortraitAppId  = "portrait_example";
        private const string LandscapeAppId = "landscape_example";

        // -------------------------------------------------------------------------
        // Fields
        // -------------------------------------------------------------------------

        private ISmartPhoneApi? smartphoneApi;

        /// <summary>
        /// Static reference to the API, made available to CardDrawing and any
        /// other static helpers that need it without constructor injection.
        /// Set once in OnGameLaunched; never null after that point.
        /// </summary>
        internal static ISmartPhoneApi? Api;

        // Portrait app icon textures (one per theme)
        private Texture2D? portraitIcon_Default;
        private Texture2D? portraitIcon_Theme2;

        // Portrait 4x2 widget textures (one per theme)
        private Texture2D? portrait4x2_Default;
        private Texture2D? portrait4x2_Theme2;

        // Landscape app icon textures (one per theme)
        private Texture2D? landscapeIcon_Default;
        private Texture2D? landscapeIcon_Theme2;

        // Landscape 4x2 widget textures (one per theme)
        private Texture2D? landscape4x2_Default;
        private Texture2D? landscape4x2_Theme2;

        // Shared particle animation state for the 4x2 live section.
        // One instance is enough – both apps can share the same animation state,
        // or you can create separate instances per app if you prefer.
        private readonly WidgetAnimationState portraitAnimation  = new();
        private readonly WidgetAnimationState landscapeAnimation = new();

        // Contactable NPC tracking.
        // We subscribe to api.ContactableNpcsChanged so this list is always current.
        // The PortraitAppScreen reads the count via the getContactCount delegate.
        private List<string> contactableNpcs = new();

        // -------------------------------------------------------------------------
        // SMAPI Entry Point
        // -------------------------------------------------------------------------

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked  += this.OnUpdateTicked;
        }

        // -------------------------------------------------------------------------
        // Event Handlers
        // -------------------------------------------------------------------------

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Step 1: Fetch the Smartphone API.
            // GetApi<T> resolves the interface type from the mod with the given ID.
            this.smartphoneApi = this.Helper.ModRegistry.GetApi<ISmartPhoneApi>(SmartphoneModId);

            if (this.smartphoneApi == null)
            {
                this.Monitor.Log("Smartphone API is unavailable. Example apps were not registered.", LogLevel.Warn);
                return;
            }

            // Publish the API on the static field so CardDrawing and other
            // static helpers can reach it without needing constructor injection.
            ModEntry.Api = this.smartphoneApi;

            // Step 2: Load all icon/widget textures.
            this.LoadTextures();

            // Step 3: Register both apps.
            this.RegisterPortraitApp();
            this.RegisterLandscapeApp();

            // Step 4: Subscribe to the contactable-NPC list so we always have
            // the current count available for the Portrait app's contact action.
            this.smartphoneApi.ContactableNpcsChanged += (npcs) => this.contactableNpcs = npcs;

            // Step 5: Register an example contact action card.
            // This adds an "Open App" button on every NPC's contact info screen.
            this.RegisterContactActionCard();
        }

        /// <summary>
        /// Called every game tick. We use this to advance the 4x2 widget animation.
        /// The widget draw callback is a passive draw-only call, so we update state
        /// here and simply render it in the draw callback.
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Advance the particle animations using the current game time.
            this.portraitAnimation.Update(Game1.currentGameTime);
            this.landscapeAnimation.Update(Game1.currentGameTime);
        }

        // -------------------------------------------------------------------------
        // Texture Loading
        // -------------------------------------------------------------------------

        private void LoadTextures()
        {
            // Portrait app assets
            //   assets/portrait_app/default/1x1.png  – default theme icon
            //   assets/portrait_app/default/4x2.png  – default theme 4x2 widget
            //   assets/portrait_app/theme2/1x1.png   – theme2 icon
            //   assets/portrait_app/theme2/4x2.png   – theme2 4x2 widget
            this.portraitIcon_Default  = TryLoad<Texture2D>("assets/portrait_app/default/1x1.png");
            this.portrait4x2_Default   = TryLoad<Texture2D>("assets/portrait_app/default/4x2.png");
            this.portraitIcon_Theme2   = TryLoad<Texture2D>("assets/portrait_app/theme2/1x1.png");
            this.portrait4x2_Theme2    = TryLoad<Texture2D>("assets/portrait_app/theme2/4x2.png");

            // Landscape app assets
            this.landscapeIcon_Default = TryLoad<Texture2D>("assets/landscape_app/default/1x1.png");
            this.landscape4x2_Default  = TryLoad<Texture2D>("assets/landscape_app/default/4x2.png");
            this.landscapeIcon_Theme2  = TryLoad<Texture2D>("assets/landscape_app/theme2/1x1.png");
            this.landscape4x2_Theme2   = TryLoad<Texture2D>("assets/landscape_app/theme2/4x2.png");
        }

        /// <summary>Helper that loads a mod content asset, logging a warning on failure.</summary>
        private T? TryLoad<T>(string relativePath) where T : class
        {
            try
            {
                return this.Helper.ModContent.Load<T>(relativePath);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not load asset '{relativePath}': {ex.Message}", LogLevel.Warn);
                return null;
            }
        }

        // -------------------------------------------------------------------------
        // App Registration
        // -------------------------------------------------------------------------

        private void RegisterPortraitApp()
        {
            if (this.smartphoneApi == null) return;

            // The icon for the "default" theme is required.
            // If it is missing, fall back to a built-in phone icon so the app still shows.
            Texture2D icon = this.portraitIcon_Default
                          ?? this.smartphoneApi.GetAppTexture(AppIconType.AppStore)
                          ?? CreateSolidTexture(Color.DodgerBlue);

            Texture2D icon2 = this.portraitIcon_Theme2 ?? icon;

            bool registered = this.smartphoneApi.RegisterPhoneApp(
                ownerModId:        this.ModManifest.UniqueID,
                appId:             PortraitAppId,
                displayName:       "Portrait Example",
                onClick:           this.OpenPortraitApp,
                closePhoneOnLaunch: true,

                // Widget sizes this app supports.
                // Size1x1 – uses the 1x1 icon texture (drawn by the framework).
                // Size2x2 – we return the same 1x1 texture; the framework scales it up.
                // Size4x2 – we handle drawing ourselves in onDrawWidget.
                supportedSizes: new[] { AppSize.Size1x1, AppSize.Size2x2, AppSize.Size4x2 },

                // Widget draw callback. Only called for sizes we listed above.
                onDrawWidget: (SpriteBatch b, Rectangle dest, AppSize size) =>
                    this.DrawPortraitWidget(b, dest, size),

                // Provide both theme textures. The framework reads the key name to
                // show available themes in the Settings app.
                themedIconTextures: new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
                {
                    { "default", icon  },
                    { "theme2",  icon2 },
                });

            if (!registered)
                this.Monitor.Log("Failed to register Portrait Example app.", LogLevel.Warn);
        }

        private void RegisterLandscapeApp()
        {
            if (this.smartphoneApi == null) return;

            Texture2D icon = this.landscapeIcon_Default
                          ?? this.smartphoneApi.GetAppTexture(AppIconType.Calendar)
                          ?? CreateSolidTexture(Color.MediumPurple);

            Texture2D icon2 = this.landscapeIcon_Theme2 ?? icon;

            bool registered = this.smartphoneApi.RegisterPhoneApp(
                ownerModId:        this.ModManifest.UniqueID,
                appId:             LandscapeAppId,
                displayName:       "Landscape Example",
                onClick:           this.OpenLandscapeApp,
                closePhoneOnLaunch: true,
                supportedSizes: new[] { AppSize.Size1x1, AppSize.Size2x2, AppSize.Size4x2 },
                onDrawWidget: (SpriteBatch b, Rectangle dest, AppSize size) =>
                    this.DrawLandscapeWidget(b, dest, size),
                themedIconTextures: new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
                {
                    { "default", icon  },
                    { "theme2",  icon2 },
                });

            if (!registered)
                this.Monitor.Log("Failed to register Landscape Example app.", LogLevel.Warn);
        }

        // -------------------------------------------------------------------------
        // App Launchers
        // -------------------------------------------------------------------------

        private void OpenPortraitApp()
        {
            if (!Context.IsWorldReady || this.smartphoneApi == null) return;

            // Open the portrait screen.
            // getContactCount is a delegate that returns the live contactable NPC count,
            // maintained by the ContactableNpcsChanged subscription in OnGameLaunched.
            Game1.activeClickableMenu = new PortraitApp.PortraitAppScreen(
                api:             this.smartphoneApi,
                onBack:          () => this.smartphoneApi.OpenPhoneHomeScreen(),
                getContactCount: () => this.contactableNpcs.Count);
        }

        // -------------------------------------------------------------------------
        // Contact Action Card Registration
        // -------------------------------------------------------------------------

        /// <summary>
        /// Registers an "Open App" button that appears on every NPC's contact info
        /// screen inside the Smartphone Contacts app.
        ///
        /// Steps:
        ///  1. Create one or more ExampleContactButton instances.
        ///  2. Call api.RegisterContactActionCard with your mod ID, a card title,
        ///     the list of buttons, and optionally a list of NPC internal names
        ///     (null = available for ALL NPCs).
        ///
        /// The OnClick callback receives the NPC's internal name as a string.
        /// </summary>
        private void RegisterContactActionCard()
        {
            if (this.smartphoneApi == null) return;

            var openButton = new ExampleContactButton
            {
                Text            = "Open Example App",
                BackgroundColor = new Color(60, 130, 220),
                TextColor       = Color.White,

                // OnClick receives the NPC internal name (e.g. "Abigail", "Leah").
                // Here we just open the portrait app regardless of which NPC was tapped.
                OnClick = (npcName) => this.OpenPortraitApp(),
            };

            bool ok = this.smartphoneApi.RegisterContactActionCard(
                modId:     this.ModManifest.UniqueID,
                cardTitle: "Example App",
                buttons:   new List<IContactActionCardButton> { openButton },
                npcNames:  null);   // null = show for every NPC

            if (!ok)
                this.Monitor.Log("Failed to register contact action card.", LogLevel.Warn);
        }

        private void OpenLandscapeApp()
        {
            if (!Context.IsWorldReady || this.smartphoneApi == null) return;

            Game1.activeClickableMenu = new LandscapeApp.LandscapeAppScreen(
                api:    this.smartphoneApi,
                onBack: () => this.smartphoneApi.OpenPhoneHomeScreen());
        }

        // -------------------------------------------------------------------------
        // Widget Drawing
        // -------------------------------------------------------------------------

        /// <summary>
        /// Draws the Portrait app widget for all three supported sizes.
        ///
        /// Widget sizing rules:
        ///  - 1x1:  The framework calls this with a small square dest rect.
        ///            We just draw the themed icon stretched to fill it.
        ///  - 2x2:  Same icon, larger dest rect – the icon naturally scales.
        ///  - 4x2:  Left half = themed 4x2.png texture.
        ///            Right half = live floating-letters animation.
        /// </summary>
        private void DrawPortraitWidget(SpriteBatch b, Rectangle dest, AppSize size)
        {
            // Resolve which theme set of textures is currently active.
            // The framework stores the active theme per composite ID.
            // We read it back so the widget matches whatever the user selected.
            string compositeId = $"{this.ModManifest.UniqueID}::{PortraitAppId}";
            string activeTheme = this.smartphoneApi?.GetComponentTheme(compositeId) ?? "default";

            bool useTheme2 = activeTheme.Equals("theme2", StringComparison.OrdinalIgnoreCase);
            Texture2D? icon  = useTheme2 ? this.portraitIcon_Theme2  : this.portraitIcon_Default;
            Texture2D? tex4x2 = useTheme2 ? this.portrait4x2_Theme2  : this.portrait4x2_Default;

            if (size == AppSize.Size4x2)
            {
                // Draw the 4x2 texture as the FULL widget background.
                if (tex4x2 != null && !tex4x2.IsDisposed)
                    b.Draw(tex4x2, dest, Color.White);
                else if (icon != null && !icon.IsDisposed)
                    b.Draw(icon, dest, Color.White);

                // Overlay the live particle animation on the RIGHT HALF only,
                // on top of the texture background.
                int halfWidth   = dest.Width / 2;
                Rectangle rightHalf = new(dest.X + halfWidth, dest.Y, dest.Width - halfWidth, dest.Height);
                this.portraitAnimation.Draw(b, rightHalf);
            }
            else
            {
                // 1x1 and 2x2: just draw the icon scaled to fill the dest rect.
                if (icon != null && !icon.IsDisposed)
                    b.Draw(icon, dest, Color.White);
            }
        }

        /// <summary>
        /// Draws the Landscape app widget. Logic mirrors DrawPortraitWidget but uses
        /// the landscape texture set and the landscape animation instance.
        /// </summary>
        private void DrawLandscapeWidget(SpriteBatch b, Rectangle dest, AppSize size)
        {
            string compositeId = $"{this.ModManifest.UniqueID}::{LandscapeAppId}";
            string activeTheme = this.smartphoneApi?.GetComponentTheme(compositeId) ?? "default";

            bool useTheme2 = activeTheme.Equals("theme2", StringComparison.OrdinalIgnoreCase);
            Texture2D? icon   = useTheme2 ? this.landscapeIcon_Theme2  : this.landscapeIcon_Default;
            Texture2D? tex4x2 = useTheme2 ? this.landscape4x2_Theme2   : this.landscape4x2_Default;

            if (size == AppSize.Size4x2)
            {
                // Draw the 4x2 texture as the FULL widget background.
                if (tex4x2 != null && !tex4x2.IsDisposed)
                    b.Draw(tex4x2, dest, Color.White);
                else if (icon != null && !icon.IsDisposed)
                    b.Draw(icon, dest, Color.White);

                // Overlay the live particle animation on the RIGHT HALF only.
                int halfWidth   = dest.Width / 2;
                Rectangle rightHalf = new(dest.X + halfWidth, dest.Y, dest.Width - halfWidth, dest.Height);
                this.landscapeAnimation.Draw(b, rightHalf);
            }
            else
            {
                if (icon != null && !icon.IsDisposed)
                    b.Draw(icon, dest, Color.White);
            }
        }

        // -------------------------------------------------------------------------
        // Utilities
        // -------------------------------------------------------------------------

        /// <summary>Creates a 84x84 solid-colour Texture2D as a last-resort fallback icon.</summary>
        private static Texture2D CreateSolidTexture(Color color)
        {
            var tex = new Texture2D(Game1.graphics.GraphicsDevice, 84, 84);
            Color[] data = new Color[84 * 84];
            for (int i = 0; i < data.Length; i++) data[i] = color;
            tex.SetData(data);
            return tex;
        }
    }
}
