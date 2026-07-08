// =============================================================================
// ISmartPhoneApi.cs  –  Local copy of the Smartphone framework public API
// =============================================================================
// Keep this file in sync with the framework's Api/ISmartPhoneApi.cs.
// This copy lives inside the mod project so it can compile without a DLL
// reference to the framework itself.
// =============================================================================

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace SmartphoneExampleApps.Data
{
    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    /// <summary>Built-in phone app icon types you can retrieve via GetAppTexture.</summary>
    public enum AppIconType
    {
        Notification,
        AppStore,
        Camera,
        Photo,
        Setting,
        Calendar
    }

    /// <summary>Widget size options that an app icon can be placed in on the home screen.</summary>
    public enum AppSize
    {
        Size1x1,
        Size2x1,
        Size2x2,
        Size2x3,
        Size2x4,
        Size4x2,
        Size4x3,
        Size4x4,
    }

    // -------------------------------------------------------------------------
    // Supporting types
    // -------------------------------------------------------------------------

    public class SelectedPhotoResult
    {
        public string AbsolutePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public byte[]? TextureData { get; set; }
    }

    public interface IContactActionCardButton
    {
        string Text { get; set; }
        Color BackgroundColor { get; set; }
        Color TextColor { get; set; }
        Action<string>? OnClick { get; set; }
    }

    // -------------------------------------------------------------------------
    // Main API interface
    // -------------------------------------------------------------------------

    public interface ISmartPhoneApi
    {
        // =====================================================================
        // 1. App & Widget Registration
        // =====================================================================

        /// <summary>
        /// Registers a custom app icon on the smartphone home screen.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app (e.g. d5a1lamdtd.markettown).</param>
        /// <param name="appId">A unique app ID within the owner mod (e.g. portrait_example).</param>
        /// <param name="displayName">Name shown as a label under the app icon.</param>
        /// <param name="onClick">Callback invoked when the app icon is clicked.</param>
        /// <param name="closePhoneOnLaunch">Whether the phone menu should close before invoking onClick.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet.</param>
        /// <param name="getBadgeCount">Optional callback to draw a badge count on the icon.</param>
        /// <param name="supportedSizes">Widget sizes this app supports. Defaults to Size1x1 when null or empty.</param>
        /// <param name="onDrawWidget">Callback for custom widget drawing: (SpriteBatch, Rectangle destinationRect, AppSize size).</param>
        /// <param name="themedIconTextures">Dictionary mapping theme name to icon Texture2D. Key "default" is always required.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Action onClick,
            bool closePhoneOnLaunch = true,
            Rectangle? sourceRect = null,
            Func<int>? getBadgeCount = null,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null
        );

        /// <summary>Unregisters a previously registered custom app.</summary>
        bool UnregisterPhoneApp(string ownerModId, string appId);

        /// <summary>Gets the resolved 1x1 icon texture for an app.</summary>
        Texture2D? GetAppIconTexture(string appId);

        /// <summary>Gets the texture of a built-in app icon.</summary>
        Texture2D? GetAppTexture(AppIconType appIconType);

        // =====================================================================
        // 2. Phone Navigation & Positioning
        // =====================================================================

        /// <summary>Opens the smartphone home (landing) screen for the current player.</summary>
        bool OpenPhoneHomeScreen();

        /// <summary>
        /// Gets the current on-screen position (top-left corner) of the phone menu.
        /// Use this when opening a custom app screen so it appears seamlessly at the
        /// same location as the phone was.
        /// </summary>
        (int x, int y) GetPhonePosition();

        /// <summary>
        /// Updates the phone position in the framework so all subsequent screens
        /// remain in sync. Call this inside your drag handling after moving the phone.
        /// </summary>
        void SetPhonePosition(int x, int y);

        /// <summary>
        /// Handles clicks on the phone's built-in bottom navigation buttons (back, home).
        /// Call this first in your receiveLeftClick override.
        /// </summary>
        bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null);

        // =====================================================================
        // 3. Theme & Appearance
        // =====================================================================

        /// <summary>Gets the current phone UI scale factor (0.75 small, 1.0 regular).</summary>
        float GetPhoneUiScale();

        /// <summary>Gets the current scaled phone frame width in pixels.</summary>
        int GetPhoneFrameWidth();

        /// <summary>Gets the current scaled phone frame height in pixels.</summary>
        int GetPhoneFrameHeight();

        /// <summary>Gets the content area offset from the top-left of the phone frame.</summary>
        (int offsetX, int offsetY) GetPhoneContentOffset();

        /// <summary>Gets the phone_empty.png border texture from the active theme.</summary>
        Texture2D? GetPhoneFrameTexture();

        /// <summary>Gets the phone_background.png wallpaper texture from the active theme.</summary>
        Texture2D? GetPhoneBackgroundTexture();

        /// <summary>Gets the card_texture.png texture from the active theme.</summary>
        Texture2D? GetCardTexture();

        /// <summary>Sets the theme for a component (identified by its composite app ID).</summary>
        void SetComponentTheme(string component, string theme);

        /// <summary>Gets the current theme name for a component.</summary>
        string GetComponentTheme(string component);

        // =====================================================================
        // 4. Size Control Settings
        // =====================================================================

        /// <summary>
        /// Draws the + and – phone scale buttons if they are enabled in config.
        /// Call this at the end of your draw() override.
        /// </summary>
        /// <param name="landscape">Pass true when drawing in landscape rotation so buttons are repositioned correctly.</param>
        /// <param name="forceOn">Force the buttons to show even if disabled in config.</param>
        void DrawPhoneSizeButtons(SpriteBatch b, int phoneX, int phoneY, bool landscape = false, bool forceOn = false);

        /// <summary>
        /// Handles clicks on the + and – phone scale buttons.
        /// Returns true if a button was clicked (swallow the click event).
        /// </summary>
        bool HandlePhoneSizeButtonsClick(int x, int y, int phoneX, int phoneY);

        /// <summary>Gets the key name that decreases the phone size.</summary>
        string GetDecreaseSizeKey();

        /// <summary>Gets the key name that increases the phone size.</summary>
        string GetIncreaseSizeKey();

        /// <summary>Adjusts the phone size by the given amount (positive = larger, negative = smaller).</summary>
        void AdjustPhoneSize(float amount);

        // =====================================================================
        // 5. Contacts App
        // =====================================================================

        bool RegisterContactActionCard(
            string modId,
            string cardTitle,
            IList<IContactActionCardButton> buttons,
            List<string> npcNames = null);

        // =====================================================================
        // 6. Notifications
        // =====================================================================

        /// <summary>Sends a notification to the player's smartphone.</summary>
        void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "");

        // =====================================================================
        // 7. Photo App
        // =====================================================================

        string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null,
            bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null,
            float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null);

        Texture2D GetPlayerPhotoTexture(string photoName);
        string GetPlayerPhotoMetadata(string photoName);

        void RetrievePhotos(int limit, bool getTexture, bool getMetadata,
            Action<string> onComplete, bool squareOnly = false);

        /// <summary>
        /// Registers draw and update callbacks for the passive HUD preview mode of an app.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app.</param>
        /// <param name="appId">The app ID that was used during registration.</param>
        /// <param name="onDrawHudScreen">Callback to draw the screen content inside the HUD phone frame.</param>
        /// <param name="onUpdateHudScreen">Optional callback to update live data/animations in passive mode.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPassiveHudCallback(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle> onDrawHudScreen,
            Action<GameTime>? onUpdateHudScreen = null
        );
    }
}
