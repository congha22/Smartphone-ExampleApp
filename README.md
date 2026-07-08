# Smartphone Developer Integration Guide

This guide provides instructions and reference documentation for Stardew Valley mod developers who want to integrate with the **Smartphone** framework. 

Using the Smartphone API, you can:
- Register custom apps with custom home screen icons (including themed support).
- Create home screen widgets in various sizes (`1x1` to `4x4`).
- Transition seamlessly from the home screen into your own custom UI menu.
- Integrate your app with the live, animated **Passive HUD Preview** (pinned mode), supporting both portrait and landscape orientations.
- Extend the in-game Contacts app with custom action cards on NPC profile pages.
- Send customized smartphone notifications to the player.
- Access the phone's Photo Album and invoke the Photo Selection menu.

---

## Table of Contents
1. [Getting Started (API Reference)](#1-getting-started-api-reference)
2. [Registering an App & Widgets](#2-registering-an-app--widgets)
3. [Building a Seamless Custom App Menu (`IClickableMenu`)](#3-building-a-seamless-custom-app-menu-iclickablemenu)
4. [Live Passive HUD Preview ("Pinning")](#4-live-passive-hud-preview-pinning)
5. [Extending the Contacts App](#5-extending-the-contacts-app)
6. [Notifications API](#6-notifications-api)
7. [Photo Selection API](#7-photo-selection-api)

---

## 1. Getting Started (API Reference)

To use the API, copy the [ISmartPhoneApi.cs](ISmartPhoneApi.cs) interface into your project (retaining its namespace or wrapping it as desired). In your mod's `Entry` method, wait for the `GameLoop.GameLaunched` event to fetch the API:

```csharp
private ISmartPhoneApi? SmartphoneApi;

private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    this.SmartphoneApi = this.Helper.ModRegistry.GetApi<ISmartPhoneApi>("congha22.Smartphone");
    if (this.SmartphoneApi == null)
    {
        this.Monitor.Log("Smartphone mod is not installed; integration disabled.", LogLevel.Info);
        return;
    }
    
    // Register your app, widgets, or callbacks here
    this.RegisterMyCustomApp();
}
```

---

## 2. Registering an App & Widgets

Use `RegisterPhoneApp` to add your app icon to the home screen. You can also specify supported widget sizes and custom draw callbacks to render live content directly on the home screen.

### Method Signature
```csharp
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
```

### Supported Widget Sizes
Widgets are placed in grid cells. The following `AppSize` values are supported:
* `Size1x1` (Standard Icon)
* `Size2x1`, `Size2x2`, `Size2x3`, `Size2x4`
* `Size4x2`, `Size4x3`, `Size4x4`

### Example Registration
```csharp
this.SmartphoneApi.RegisterPhoneApp(
    ownerModId: this.ModManifest.UniqueID,
    appId: "myAppId",
    displayName: "My Mod App",
    onClick: () => {
        // Open your custom menu here
        Game1.activeClickableMenu = new MyCustomAppScreen(this.SmartphoneApi);
    },
    closePhoneOnLaunch: false, // Keep phone framework state active for transition
    supportedSizes: new[] { AppSize.Size1x1, AppSize.Size2x2 },
    onDrawWidget: (spriteBatch, bounds, size) => {
        if (size == AppSize.Size2x2)
        {
            // Draw custom widget content (e.g., status logs, dynamic text)
            spriteBatch.Draw(Game1.staminaRect, bounds, Color.DarkSlateGray);
            Utility.drawTextWithShadow(spriteBatch, "Live Info", Game1.smallFont, new Vector2(bounds.X + 10, bounds.Y + 10), Color.White);
        }
    }
);
```

---

## 3. Building a Seamless Custom App Menu (`IClickableMenu`)

To make your app look and feel like it is running "inside" the smartphone, you should mimic the bezel, wallpaper background, and scaling features. 

### Essential Layout Rules

1. **Position Syncing:** 
   Get the current position from the framework so transitions from the home screen are seamless. If the player drags your app screen, save the new coordinate back to the framework using `SetPhonePosition`:
   ```csharp
   // On initialization:
   var (x, y) = this.smartphoneApi.GetPhonePosition();
   this.xPositionOnScreen = x;
   this.yPositionOnScreen = y;
   
   // On drag/update:
   this.smartphoneApi.SetPhonePosition(this.xPositionOnScreen, this.yPositionOnScreen);
   ```

2. **Scaling & Sizing:**
   Retrieve dimensions and offsets dynamically. Never hardcode absolute sizes because the phone can be toggled to a "Small Size" scale:
   ```csharp
   float scale = this.smartphoneApi.GetPhoneUiScale(); // 1.0f or 0.75f
   int phoneWidth = this.smartphoneApi.GetPhoneFrameWidth();
   int phoneHeight = this.smartphoneApi.GetPhoneFrameHeight();
   
   var (contentOffsetX, contentOffsetY) = this.smartphoneApi.GetPhoneContentOffset();
   
   // The active screen boundaries inside the bezel:
   Rectangle screenContentRect = new Rectangle(
       this.xPositionOnScreen + contentOffsetX,
       this.yPositionOnScreen + contentOffsetY,
       phoneWidth - (contentOffsetX * 2), // Note: fits portrait width
       phoneHeight - contentOffsetY - bottomNavOffset
   );
   ```

3. **Drawing Phone Elements:**
   Always draw the background first, then your app content, then the bezel frame on top:
   ```csharp
   Texture2D? background = this.smartphoneApi.GetPhoneBackgroundTexture();
   Texture2D? bezel = this.smartphoneApi.GetPhoneFrameTexture();
   
   // 1. Background wallpaper
   if (background != null)
       spriteBatch.Draw(background, screenContentRect, Color.White);
       
   // 2. Draw your app UI (cards, text, buttons)...
   
   // 3. Phone Bezel border (draw last, on top of content)
   if (bezel != null)
       spriteBatch.Draw(bezel, new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, phoneWidth, phoneHeight), Color.White);
   ```

4. **Handling Built-in Bottom Navigation:**
   Smartphone bezel overlays home, back, and lock buttons. Call the helper click method in your menu's click handler:
   ```csharp
   public override void receiveLeftClick(int x, int y, bool playSound = true)
   {
       // Handles Back/Home/Lock clicks automatically, exiting your menu or returning home
       if (this.smartphoneApi.HandlePhoneAppBottomNavClick(x, y, this.xPositionOnScreen, this.yPositionOnScreen, onBack: () => this.GoToPreviousPage()))
           return;
           
       // Your own click checks...
   }
   ```

---

## 4. Live Passive HUD Preview ("Pinning")

Smartphone features a **Passive HUD Mode** where players can "pin" an app as a small, live icon on their gameplay HUD. The framework draws the active screen to an off-screen buffer in the background and renders it live on screen.

### Step 1: Register HUD Callback
```csharp
this.smartphoneApi.RegisterPassiveHudCallback(
    ownerModId: this.ModManifest.UniqueID,
    appId: "myAppId",
    onDrawHudScreen: (spriteBatch, targetBounds) => {
        // Forward drawing to your active screen
        this.activeScreen?.DrawScreenContent(spriteBatch, targetBounds);
    },
    onUpdateHudScreen: (gameTime) => {
        // Optional: Tick animations or physics updates
        this.activeScreen?.update(gameTime);
    },
    landscape: false // Set to true if this is a landscape app
);
```

### Step 2: Implement `DrawScreenContent` Inside Your Menu
Your screen drawing code must be encapsulated in a method that can draw independently of active menu parameters.

#### Rules for `DrawScreenContent`:
1. **Reset Scale to `1.0f`:** The passive HUD render target capture runs at scale `1.0f`. Temporarily override `phoneUiScale` to `1.0f` and run your layout refresh logic, then restore the old scale inside a `finally` block.
2. **Translate to Coordinate Origin `(0, 0)`:**
   - **For Portrait Apps:** Set coordinates to `xPositionOnScreen = -phoneContentOffsetX` and `yPositionOnScreen = -phoneContentOffsetY`.
   - **For Landscape Apps:** Set coordinates to `xPositionOnScreen = -phoneContentOffsetY` and `yPositionOnScreen = -phoneContentOffsetX`.
3. **Render Wallpaper and Pages:**
   - **For Portrait:** Draw background wallpaper normally.
   - **For Landscape:** Draw background wallpaper rotated `-MathHelper.PiOver2` at `(0, height)`.
   - Render pages/components relative to this coordinate system.
4. **Use Relative Coordinates for Physics/UI:** All positions, bounce limits, scroll offsets, or particle positions should be kept in local coordinates relative to the screen bounds `(0, 0, width, height)` instead of absolute screen space.

*Refer to [PortraitAppScreen.cs](PortraitApp/PortraitAppScreen.cs) or [LandscapeAppScreen.cs](LandscapeApp/LandscapeAppScreen.cs) for full working implementations.*

### Step 3: Screen Resume (Back to Last Screen)

When a player launches your app, they might have previously left it running in the background/HUD pin mode. To prevent resetting their progress (e.g. losing a chat thread or custom menu position), you should query the API's pinning state and reuse the cached screen instance.

Add `IsHudPinned()` and `GetPinnedAppId()` to your copy of `ISmartPhoneApi.cs`:
```csharp
bool IsHudPinned();
string? GetPinnedAppId();
```

Then in your app's entry-point launch method, check if it's launching as a resume:
```csharp
private void OpenMyCustomApp()
{
    if (!Context.IsWorldReady || this.SmartphoneApi == null) return;

    // Build the composite app ID used by the framework: "OwnerModId::AppId"
    string compositeId = $"{this.ModManifest.UniqueID}::myAppId";
    
    bool resume = this.SmartphoneApi.IsHudPinned() && string.Equals(this.SmartphoneApi.GetPinnedAppId(), compositeId);
    
    if (!resume || this.activeScreen == null)
    {
        // Instantiate a fresh screen if not resuming or activeScreen was lost
        this.activeScreen = new MyCustomAppScreen(this.SmartphoneApi);
    }
    
    Game1.activeClickableMenu = this.activeScreen;
}
```

#### Safe Update Ticking (Double-Update Bypass)
When your app screen is open as the active menu, the game's menu loop automatically updates it. To prevent calling `update()` twice per frame, bypass background updating inside your `onUpdateHudScreen` callback when the screen is the open menu:
```csharp
onUpdateHudScreen: (gameTime) => {
    if (Game1.activeClickableMenu == this.activeScreen)
        return;
        
    this.activeScreen?.update(gameTime);
}
```

---

## 5. Extending the Contacts App

You can register custom action cards that appear on the profile page of NPCs inside the built-in Contacts app. This is perfect for calling custom actions (e.g. asking out, requesting services, checking progress).

### Card Buttons Definition
Implement the `IContactActionCardButton` interface for your buttons:
```csharp
public class ActionCardButton : IContactActionCardButton
{
    public string Text { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = Color.LightSlateGray;
    public Color TextColor { get; set; } = Color.White;
    public Action<string>? OnClick { get; set; } // Parameter is NPC internal Name
}
```

### Registering Card
```csharp
var buttons = new List<IContactActionCardButton>
{
    new ActionCardButton
    {
        Text = "Request Service",
        OnClick = (npcName) => {
            Game1.activeClickableMenu?.exitThisMenu();
            HUDMessage.AddMessage(new HUDMessage($"Requested service from {npcName}!", HUDMessage.newQuest_type));
        }
    }
};

// Register for specific NPCs, or leave null to show on all contact profiles
this.SmartphoneApi.RegisterContactActionCard(
    modId: this.ModManifest.UniqueID,
    cardTitle: "My Mod Actions",
    buttons: buttons,
    npcNames: new List<string> { "Robin", "Clint" }
);
```

---

## 6. Notifications API

Send alerts directly to the player's phone. This puts a message in the phone's Notification Center app and plays a notification chime:

```csharp
this.SmartphoneApi.SendSmartphoneNotification(
    message: "Your processing machine has completed its run!",
    notificationName: "Machine Monitor", // Optional title
    playerId: "" // Optional multiplayer ID. Leave blank to target the local player.
);
```

---

## 7. Photo Selection API

You can open the phone's Photo Album in selection mode, letting the player choose one or more images from their camera rolls. The callback returns a JSON list of files with their paths and binary texture data.

```csharp
this.SmartphoneApi.RetrievePhotos(
    limit: 1,           // Maximum photos they can select
    getTexture: true,   // If true, populates SelectedPhotoResult.TextureData
    getMetadata: true,  // If true, populates date/time metadata
    squareOnly: false,  // Only show square-cropped photos
    onComplete: (jsonResult) => {
        // Deserialize jsonResult to List<SelectedPhotoResult>
        var photos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SelectedPhotoResult>>(jsonResult);
        if (photos != null && photos.Count > 0)
        {
            var selected = photos[0];
            this.Monitor.Log($"Player selected photo: {selected.FileName} at {selected.AbsolutePath}", LogLevel.Info);
        }
    }
);
```

---

## Example Projects
This workspace contains two reference app projects:
- **Portrait App:** An example portrait layout application demonstrating scale transformations, badge count, size button drawing, and dynamic bouncing ball preview inside the passive HUD.
- **Landscape App:** An example landscape layout application showing how to handle landscape dimensions (`810x520` at `1.0f`), rotated background drawing, and horizontal page bounds alignment.
