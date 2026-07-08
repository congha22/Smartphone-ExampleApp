// =============================================================================
// PortraitAppScreen.cs  –  Portrait App Screen
// =============================================================================
// This screen demonstrates a standard PORTRAIT phone app.
//
// Features shown:
//  • Reading phone position, frame size, content offset, and UI scale from the API.
//  • Dynamically updating the layout when the scale changes (phone size hotkeys).
//  • Drawing the phone background and border frame.
//  • Drawing the + / − size-adjustment buttons on the bezel.
//  • Dragging the phone around the screen and syncing position with the framework.
//  • Scrolling content vertically (scroll wheel + touch drag).
//  • Swiping between pages horizontally (touch swipe left/right).
//  • Sending notifications via the Smartphone notification API.
//  • Opening the Photo picker to select a photo and retrieving its name.
//  • Reading the contactable NPC list count.
//  • Bottom navigation bar handled by the framework.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SmartphoneExampleApps.Data;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneExampleApps.PortraitApp
{
    /// <summary>
    /// A portrait-orientation custom app screen.  Inherits from IClickableMenu so
    /// SMAPI/Stardew handle it as a full-screen overlay just like any other menu.
    /// </summary>
    public class PortraitAppScreen : IClickableMenu, IKeyboardSubscriber
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------

        private readonly ISmartPhoneApi api;

        /// <summary>Called when the user presses Back/Escape to return to the home screen.</summary>
        private readonly Action onBack;

        // -------------------------------------------------------------------------
        // Page 2 – Multiline Editable TextBox
        // -------------------------------------------------------------------------
        private readonly EditableTextBox customTextBox = new() { IsMultiline = true };
        private Rectangle textBoxBounds;
        private bool textBoxFocused = false;

        public bool Selected { get; set; } = true;

        // -------------------------------------------------------------------------
        // Phone layout  (recalculated whenever the scale changes)
        // -------------------------------------------------------------------------

        private float phoneUiScale;
        private int   phoneFrameWidth;
        private int   phoneFrameHeight;
        private int   phoneContentOffsetX;
        private int   phoneContentOffsetY;

        private int   contentWidth;   // usable screen area inside the phone bezel
        private int   contentHeight;

        private Texture2D? phoneFrameTexture;
        private Texture2D? phoneBackgroundTexture;

        // -------------------------------------------------------------------------
        // Drag state  (dragging the phone by its bezel)
        // -------------------------------------------------------------------------

        private bool isDragging;
        private int  dragOffsetX;
        private int  dragOffsetY;

        // -------------------------------------------------------------------------
        // Horizontal swipe state  (switching between pages)
        // -------------------------------------------------------------------------

        private int   currentPage;
        private const int TotalPages = 3;

        /// <summary>Fractional page offset for the swipe animation (0 = on current page).</summary>
        private float pageScrollX  = 0f;

        /// <summary>
        /// The page we are animating toward after the user lifts their finger.
        /// Committed in releaseLeftClick; the update() loop lerps pageScrollX to reach it.
        /// </summary>
        private int swipeTargetPage = 0;

        private bool  isSwiping;
        private int   swipeStartX;

        // -------------------------------------------------------------------------
        // Vertical scroll state  (page 0 – text lines)
        // -------------------------------------------------------------------------

        private int  vertScrollOffset  = 0;
        private int  maxVertScroll     = 0;
        private bool isVertScrolling;
        private int  vertScrollStartY;
        private int  lastVertScrollMouseY;
        private bool hasVertScrolled;

        // -------------------------------------------------------------------------
        // Page 1 – scrollable text lines
        // -------------------------------------------------------------------------

        /// <summary>
        /// The 99 lines of text content.  Generated once in the constructor.
        /// Having many lines forces the user to scroll, demonstrating the scroll
        /// feature and also showing how text size changes with the phone scale.
        /// </summary>
        private readonly List<string> textLines = new();

        // -------------------------------------------------------------------------
        // Page 2 – API action buttons
        // -------------------------------------------------------------------------

        private readonly List<(Rectangle Rect, string Label, Color Color)> actionButtons = new();
        private int hoveredActionButton = -1;

        // -------------------------------------------------------------------------
        // Popup feedback (shown after page-2 actions complete)
        // -------------------------------------------------------------------------

        /// <summary>Message to display in the popup panel. Empty = no popup shown.</summary>
        private string popupMessage = string.Empty;

        /// <summary>Seconds remaining before the popup auto-hides.</summary>
        private float popupTimer = 0f;

        private const float PopupDuration = 4f;

        // -------------------------------------------------------------------------
        // Page 3 – bouncing ball
        // -------------------------------------------------------------------------

        private Vector2 ballPos;
        private Vector2 ballVelocity;
        private float   ballRadius;
        private float   bounceTimer = 0f;

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------

        /// <param name="api">Smartphone framework API reference.</param>
        /// <param name="onBack">Callback to close this screen and return to home.</param>
        /// <param name="initialPopup">Optional message to show immediately when the screen opens.</param>
        /// <param name="initialPage">Which page to open on (default 0).</param>
        public PortraitAppScreen(
            ISmartPhoneApi api,
            Action onBack,
            string? initialPopup = null,
            int    initialPage   = 0)
            : base()
        {
            this.api             = api;
            this.onBack          = onBack;
            this.currentPage     = initialPage;

            // Seed the popup if something needs to be shown right away
            // (e.g. photo picker closed and returned a result).
            if (!string.IsNullOrEmpty(initialPopup))
            {
                this.popupMessage = initialPopup;
                this.popupTimer   = PopupDuration;
            }

            // Read the current phone position from the framework so our screen
            // appears at the same location as the phone was when the app was launched.
            var (px, py) = api.GetPhonePosition();
            this.xPositionOnScreen = px;
            this.yPositionOnScreen = py;

            // Build layout and content for all pages.
            // IMPORTANT: BuildTextLines() must run BEFORE RefreshLayout() so that
            // RecalcScrollBounds() sees the 99 text lines and computes a nonzero maxVertScroll.
            this.BuildTextLines();
            this.RefreshLayout();
            this.ResetBall();
        }

        // -------------------------------------------------------------------------
        // Layout helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Reads all scale-dependent values from the API and rebuilds every layout
        /// rectangle.  Called on construction and whenever api.GetPhoneUiScale() changes.
        /// </summary>
        private void RefreshLayout()
        {
            this.phoneUiScale      = this.api.GetPhoneUiScale();
            this.phoneFrameWidth   = this.api.GetPhoneFrameWidth();
            this.phoneFrameHeight  = this.api.GetPhoneFrameHeight();

            var (ox, oy) = this.api.GetPhoneContentOffset();
            this.phoneContentOffsetX = ox;
            this.phoneContentOffsetY = oy;

            this.phoneFrameTexture      = this.api.GetPhoneFrameTexture();
            this.phoneBackgroundTexture = this.api.GetPhoneBackgroundTexture();

            // IClickableMenu expects width/height to cover the full phone frame.
            this.width  = this.phoneFrameWidth;
            this.height = this.phoneFrameHeight;

            // Content area sits inside the bezel.
            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
            {
                this.contentWidth  = (int)Math.Round(this.phoneBackgroundTexture.Width  * this.phoneUiScale);
                this.contentHeight = (int)Math.Round(this.phoneBackgroundTexture.Height * this.phoneUiScale);
            }
            else
            {
                this.contentWidth  = Math.Max(1, this.phoneFrameWidth  - this.phoneContentOffsetX * 2);
                this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - Scale(80));
            }

            this.RecalcScrollBounds();
            this.RebuildActionButtons();

            // Layout the multiline text box below the 3 action buttons on page 2.
            int btnH = Scale(52);
            int gap = Scale(12);
            int startX = this.xPositionOnScreen + this.phoneContentOffsetX + Scale(12);
            int startY = this.yPositionOnScreen + this.phoneContentOffsetY + Scale(44);
            int btnW = this.contentWidth - Scale(24);

            int textBoxY = startY + 3 * (btnH + gap);
            int textBoxH = Math.Max(Scale(60), this.yPositionOnScreen + this.phoneContentOffsetY + this.contentHeight - textBoxY - Scale(16));
            this.textBoxBounds = new Rectangle(startX, textBoxY, btnW, textBoxH);
        }

        /// <summary>Scales a base pixel value by the current phone UI scale.</summary>
        private int Scale(int v) => (int)Math.Round(v * this.phoneUiScale);

        private Rectangle FrameRect =>
            new(this.xPositionOnScreen, this.yPositionOnScreen,
                this.phoneFrameWidth, this.phoneFrameHeight);

        private Rectangle ContentRect =>
            new(this.xPositionOnScreen + this.phoneContentOffsetX,
                this.yPositionOnScreen + this.phoneContentOffsetY,
                this.contentWidth, this.contentHeight);

        // -------------------------------------------------------------------------
        // Page 1  –  Text lines
        // -------------------------------------------------------------------------

        private void BuildTextLines()
        {
            this.textLines.Clear();
            for (int i = 1; i <= 99; i++)
                this.textLines.Add($"Line {i}");
        }

        private void RecalcScrollBounds()
        {
            // Line height is based on the current scale so scrolling spans change
            // proportionally when the user resizes the phone.
            int lineH   = Scale(32);
            int totalH  = this.textLines.Count * lineH + Scale(40); // header + padding
            this.maxVertScroll = Math.Max(0, totalH - this.contentHeight);
            this.vertScrollOffset = Math.Clamp(this.vertScrollOffset, 0, this.maxVertScroll);
        }

        // -------------------------------------------------------------------------
        // Page 2  –  Action buttons
        // -------------------------------------------------------------------------

        private void RebuildActionButtons()
        {
            this.actionButtons.Clear();
            Rectangle c = this.ContentRect;

            int btnH = Scale(52);
            int btnW = c.Width - Scale(24);
            int gap  = Scale(12);
            int startX = c.X + Scale(12);
            int startY = c.Y + Scale(44); // leave room for header

            var labels = new[]
            {
                "Send Notification",
                "Select 1 Photo",
                "Trigger Dummy Action",
            };
            var colors = new[]
            {
                new Color(50, 130, 220),   // blue  – notification
                new Color(60, 170, 100),   // green – photo
                new Color(180, 90, 200),   // purple – contacts
            };

            for (int i = 0; i < labels.Length; i++)
            {
                int y = startY + i * (btnH + gap);
                this.actionButtons.Add((new Rectangle(startX, y, btnW, btnH), labels[i], colors[i]));
            }
        }

        // -------------------------------------------------------------------------
        // Page 3  –  Bouncing ball
        // -------------------------------------------------------------------------

        private void ResetBall()
        {
            this.ballRadius   = Scale(18);
            this.ballPos      = new Vector2(this.contentWidth / 2f, this.contentHeight / 2f);
            this.ballVelocity = new Vector2(Scale(3), Scale(2));
        }

        // -------------------------------------------------------------------------
        // IClickableMenu  –  Update
        // -------------------------------------------------------------------------

        public override void update(GameTime time)
        {
            // -- Detect scale change and rebuild layout --
            float curScale = this.api.GetPhoneUiScale();
            if (Math.Abs(curScale - this.phoneUiScale) > 0.001f)
            {
                this.RefreshLayout();
                this.ResetBall();
            }

            base.update(time);
            this.customTextBox.Update(time, this.textBoxFocused);

            float dt = (float)time.ElapsedGameTime.TotalSeconds;
            this.bounceTimer += dt;

            // -- Popup countdown --
            if (this.popupTimer > 0f)
                this.popupTimer = Math.Max(0f, this.popupTimer - dt);

            // -- Drag: update position and sync to framework --
            if (this.isDragging)
            {
                this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
                this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
                this.RebuildActionButtons();

                // IMPORTANT: tell the framework the new phone position so
                // the home screen, settings, and other menus reopen at the same spot.
                this.api.SetPhonePosition(this.xPositionOnScreen, this.yPositionOnScreen);
            }

            // -- Swipe: lerp the scroll offset toward the target page --
            // The target page is decided the moment the finger is released
            // (stored in swipeTargetPage). We just animate pageScrollX towards it.
            if (!this.isSwiping && Math.Abs(this.pageScrollX) > 0.001f)
            {
                float targetOffset = this.swipeTargetPage - this.currentPage;
                float speed        = 10f * dt;
                if (this.pageScrollX < targetOffset)
                    this.pageScrollX = Math.Min(targetOffset, this.pageScrollX + speed);
                else
                    this.pageScrollX = Math.Max(targetOffset, this.pageScrollX - speed);

                // Snap to final page when close enough.
                if (Math.Abs(this.pageScrollX - targetOffset) < 0.01f)
                {
                    this.currentPage = this.swipeTargetPage;
                    this.pageScrollX = 0f;
                }
            }

            // -- Bouncing ball on page 3 --
            if (this.currentPage == 2)
            {
                this.ballPos += this.ballVelocity;

                if (this.ballPos.X - this.ballRadius < 0)
                { this.ballPos.X = this.ballRadius; this.ballVelocity.X = Math.Abs(this.ballVelocity.X); Game1.playSound("drumkit6"); }
                if (this.ballPos.X + this.ballRadius > this.contentWidth)
                { this.ballPos.X = this.contentWidth - this.ballRadius; this.ballVelocity.X = -Math.Abs(this.ballVelocity.X); Game1.playSound("drumkit6"); }
                if (this.ballPos.Y - this.ballRadius < 0)
                { this.ballPos.Y = this.ballRadius; this.ballVelocity.Y = Math.Abs(this.ballVelocity.Y); Game1.playSound("drumkit6"); }
                if (this.ballPos.Y + this.ballRadius > this.contentHeight)
                { this.ballPos.Y = this.contentHeight - this.ballRadius; this.ballVelocity.Y = -Math.Abs(this.ballVelocity.Y); Game1.playSound("drumkit6"); }
            }
        }

        // -------------------------------------------------------------------------
        // IClickableMenu  –  Draw
        // -------------------------------------------------------------------------

        public override void draw(SpriteBatch b)
        {
            // 1. Dim the game world behind the phone.
            b.Draw(Game1.staminaRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.55f);

            Rectangle content = this.ContentRect;
            Rectangle frame   = this.FrameRect;

            // 2. Draw the phone background / wallpaper.
            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
                b.Draw(this.phoneBackgroundTexture, content, Color.White);
            else
                b.Draw(Game1.staminaRect, content, new Color(20, 24, 36));

            // 3. Draw scrollable / swipeable content inside a scissor-clipped region.
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    null, new RasterizerState { ScissorTestEnable = true });

            Rectangle prevScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle =
                Rectangle.Intersect(content, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            this.DrawPages(b, content);

            // Draw popup inside the scissor region so it clips to the phone screen.
            if (this.popupTimer > 0f && !string.IsNullOrEmpty(this.popupMessage))
                this.DrawPopup(b, content);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = prevScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // 4. Page indicator dots.
            this.DrawPageDots(b, content);

            // 5. Phone bezel (border) on top so it overlaps content edges.
            if (this.phoneFrameTexture != null && !this.phoneFrameTexture.IsDisposed)
                b.Draw(this.phoneFrameTexture, frame, Color.White);

            // 6. Size-adjustment buttons on the bezel.
            //    landscape: false because this is a portrait screen.
            this.api.DrawPhoneSizeButtons(b, this.xPositionOnScreen, this.yPositionOnScreen, landscape: false);

            // 7. Mouse cursor always on top.
            drawMouse(b);
        }

        // -------------------------------------------------------------------------
        // Page rendering
        // -------------------------------------------------------------------------

        public void DrawScreenContent(SpriteBatch b, Rectangle content)
        {
            float oldScale = this.phoneUiScale;
            int oldX = this.xPositionOnScreen;
            int oldY = this.yPositionOnScreen;

            if (Math.Abs(this.phoneUiScale - 1f) > 0.001f)
            {
                this.phoneUiScale = 1f;
            }

            this.xPositionOnScreen = -this.phoneContentOffsetX;
            this.yPositionOnScreen = -this.phoneContentOffsetY;

            this.RefreshLayout();

            try
            {
                // 1. Draw background
                if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
                    b.Draw(this.phoneBackgroundTexture, content, Color.White);
                else
                    b.Draw(Game1.staminaRect, content, new Color(20, 24, 36));

                // 2. Draw pages
                this.DrawPages(b, content);
            }
            finally
            {
                this.phoneUiScale = oldScale;
                this.xPositionOnScreen = oldX;
                this.yPositionOnScreen = oldY;
                this.RefreshLayout();
            }
        }

        private void DrawPages(SpriteBatch b, Rectangle content)
        {
            float totalScrollX = (this.currentPage + this.pageScrollX) * this.contentWidth;

            for (int p = 0; p < TotalPages; p++)
            {
                int pageOffsetX = (int)Math.Round(p * this.contentWidth - totalScrollX);
                Rectangle pageRect = new(content.X + pageOffsetX, content.Y, this.contentWidth, this.contentHeight);

                if (pageRect.Right < content.Left - 10 || pageRect.Left > content.Right + 10)
                    continue;

                switch (p)
                {
                    case 0: this.DrawPage0_TextLines(b, pageRect); break;
                    case 1: this.DrawPage1_Actions(b, pageRect, content); break;
                    case 2: this.DrawPage2_BouncingBall(b, pageRect); break;
                }
            }
        }

        // -- Page 0: Text Lines --
        private void DrawPage0_TextLines(SpriteBatch b, Rectangle pageRect)
        {
            SpriteFont font = Game1.dialogueFont;

            // Header (fixed, not scrolled)
            string header = "← Swipe │ Scroll ↕ │ Resize +/−";
            float hs = 0.40f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(6)),
                Color.Black * 0.75f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            // Scrollable text lines.
            // Font scale is multiplied by phoneUiScale so text grows/shrinks with the phone.
            float lineScale = 0.50f * this.phoneUiScale;
            int   lineH     = Scale(32);
            int   startY    = pageRect.Y + Scale(36) - this.vertScrollOffset;

            for (int i = 0; i < this.textLines.Count; i++)
            {
                int drawY = startY + i * lineH;
                // Skip lines outside the visible content area for performance.
                if (drawY + lineH < pageRect.Y) continue;
                if (drawY > pageRect.Bottom)    break;

                // Alternate slight background tinting every other row for readability.
                if (i % 2 == 0)
                {
                    b.Draw(Game1.staminaRect,
                        new Rectangle(pageRect.X, drawY, pageRect.Width, lineH),
                        Color.White * 0.06f);
                }

                // Draw line number in accent colour, text in white.
                string lineNum = $"{i + 1,3}.  ";
                string lineText = this.textLines[i];

                Vector2 numSz = font.MeasureString(lineNum) * lineScale;
                float textX = pageRect.X + Scale(10);
                float textY = drawY + (lineH - numSz.Y) / 2f;

                // Accent number
                b.DrawString(font, lineNum, new Vector2(textX, textY),
                    new Color(20, 80, 180), 0f, Vector2.Zero, lineScale, SpriteEffects.None, 1f);

                // Line content
                b.DrawString(font, lineText,
                    new Vector2(textX + numSz.X, textY),
                    Color.Black * 0.85f, 0f, Vector2.Zero, lineScale, SpriteEffects.None, 1f);
            }

            // Down-arrow hint when not fully scrolled.
            if (this.vertScrollOffset < this.maxVertScroll)
            {
                float bounce = (float)Math.Sin(this.bounceTimer * 5f) * Scale(3);
                string arrow = "↓  more";
                float arrowScale = 0.45f * this.phoneUiScale;
                Vector2 aSz = font.MeasureString(arrow) * arrowScale;
                b.DrawString(font, arrow,
                    new Vector2(pageRect.Center.X - aSz.X / 2f, pageRect.Bottom - Scale(28) + bounce),
                    Color.Black * 0.55f, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 1f);
            }
        }

        // -- Page 1: API action buttons --
        private void DrawPage1_Actions(SpriteBatch b, Rectangle pageRect, Rectangle content)
        {
            SpriteFont font = Game1.dialogueFont;
            int shiftX = pageRect.X - content.X;

            // Header
            string header = "Page 2 – API Actions";
            float hs = 0.48f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(10)),
                Color.White * 0.85f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            // Draw each action button using the shared CardDrawing utility.
            // CardDrawing.DrawCard handles the 9-slice texture internally via ModEntry.Api.
            for (int i = 0; i < this.actionButtons.Count; i++)
            {
                var (rect, label, color) = this.actionButtons[i];
                Rectangle draw = new(rect.X + shiftX, rect.Y, rect.Width, rect.Height);
                bool hovered   = i == this.hoveredActionButton;
                Color fill     = hovered ? LightenColor(color, 0.20f) : color;

                // Draw the card background (9-slice, optional shadow on hover).
                CardDrawing.DrawCard(b, draw.X, draw.Y, draw.Width, draw.Height,
                                     fill, scale: 1f, drawShadow: hovered);

                // Draw the centred label text on top of the card.
                SpriteFont f    = Game1.dialogueFont;
                float      ts   = 0.45f * this.phoneUiScale;
                Vector2    sz   = f.MeasureString(label) * ts;
                Vector2    pos  = new(draw.Center.X - sz.X / 2f, draw.Center.Y - sz.Y / 2f);
                b.DrawString(f, label, pos + new Vector2(1f, 1f), Color.Black * 0.30f, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
                b.DrawString(f, label, pos, Color.White, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
            }

            // Draw the multiline text box container on page 2.
            Rectangle drawTextBox = new(this.textBoxBounds.X + shiftX, this.textBoxBounds.Y, this.textBoxBounds.Width, this.textBoxBounds.Height);
            
            // 9-slice card background with a light off-white color (background for typing)
            CardDrawing.DrawCard(b, drawTextBox.X, drawTextBox.Y, drawTextBox.Width, drawTextBox.Height,
                                 new Color(245, 245, 248), scale: 1f, drawShadow: this.textBoxFocused);

            // Let the text box draw its cursor and multiline text.
            this.customTextBox.Draw(b, drawTextBox, this.phoneUiScale, this.textBoxFocused);

            // Draw placeholder text if empty and not focused.
            if (string.IsNullOrEmpty(this.customTextBox.Text) && !this.textBoxFocused)
            {
                SpriteFont f = Game1.smallFont;
                float ts     = this.phoneUiScale;
                int pad      = (int)Math.Round(10 * this.phoneUiScale);
                b.DrawString(f, "Tap to type...", new Vector2(drawTextBox.X + pad, drawTextBox.Y + pad),
                             Color.Gray * 0.7f, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
            }
        }

        // -- Page 2: Bouncing ball --
        private void DrawPage2_BouncingBall(SpriteBatch b, Rectangle pageRect)
        {
            SpriteFont font = Game1.dialogueFont;

            // Dark tinted overlay over the wallpaper for contrast.
            b.Draw(Game1.staminaRect, pageRect, new Color(5, 10, 25) * 0.65f);

            string header = "Page 3 – Tap to boost ball";
            float hs = 0.43f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(8)),
                Color.Cyan * 0.85f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            int   r    = (int)this.ballRadius;
            Color ball = new(
                (int)(Math.Sin(this.bounceTimer * 2.0f) * 127 + 128),
                (int)(Math.Sin(this.bounceTimer * 3.1f + 2f) * 127 + 128),
                (int)(Math.Sin(this.bounceTimer * 4.7f + 4f) * 127 + 128));

            // Ball body is drawn relative to pageRect top-left corner
            Vector2 drawPos = new Vector2(pageRect.X, pageRect.Y) + this.ballPos;

            b.Draw(Game1.staminaRect,
                new Rectangle((int)(drawPos.X - r), (int)(drawPos.Y - r), r * 2, r * 2),
                ball);
            // Reflection highlight
            b.Draw(Game1.staminaRect,
                new Rectangle((int)(drawPos.X - r / 2), (int)(drawPos.Y - r + 3), r / 2, r / 2),
                Color.White * 0.35f);
        }

        // -- Popup overlay --
        private void DrawPopup(SpriteBatch b, Rectangle content)
        {
            float fade = Math.Min(1f, this.popupTimer / 0.5f); // quick fade-in
            SpriteFont font  = Game1.dialogueFont;
            float textScale  = 0.42f * this.phoneUiScale;

            // Word-wrap the message to fit the content width.
            string msg = WrapText(font, this.popupMessage, (content.Width - Scale(28)) / textScale, textScale);

            Vector2 tSz     = font.MeasureString(msg) * textScale;
            int padX        = Scale(12);
            int padY        = Scale(10);
            int panelW      = (int)tSz.X + padX * 2;
            int panelH      = (int)tSz.Y + padY * 2;
            int panelX      = content.Center.X - panelW / 2;
            int panelY      = content.Bottom   - panelH - Scale(10);

            // Dark semi-transparent background panel.
            b.Draw(Game1.staminaRect,
                new Rectangle(panelX, panelY, panelW, panelH),
                new Color(10, 10, 20) * (0.88f * fade));

            // Thin top border
            b.Draw(Game1.staminaRect,
                new Rectangle(panelX, panelY, panelW, 2),
                Color.CornflowerBlue * fade);

            // Message text
            b.DrawString(font, msg,
                new Vector2(panelX + padX, panelY + padY),
                Color.White * fade, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
        }

        // -- Page dots --
        private void DrawPageDots(SpriteBatch b, Rectangle content)
        {
            int dotSize   = Scale(8);
            int dotGap    = Scale(6);
            int totalDotW = TotalPages * dotSize + (TotalPages - 1) * dotGap;
            int dotY      = content.Bottom + Scale(5);
            int dotStartX = content.Center.X - totalDotW / 2;

            for (int i = 0; i < TotalPages; i++)
            {
                Color col = i == this.currentPage ? Color.White : new Color(100, 100, 100);
                b.Draw(Game1.staminaRect,
                    new Rectangle(dotStartX + i * (dotSize + dotGap), dotY, dotSize, dotSize), col);
            }
        }

        // -------------------------------------------------------------------------
        // Input – Left click (press)
        // -------------------------------------------------------------------------

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // Always check the bottom nav bar first (Back / Home buttons).
            if (this.api.HandlePhoneAppBottomNavClick(x, y,
                    this.xPositionOnScreen, this.yPositionOnScreen, onBack: this.onBack))
                return;

            // Check the size-adjustment (+/−) buttons.
            if (this.api.HandlePhoneSizeButtonsClick(x, y,
                    this.xPositionOnScreen, this.yPositionOnScreen))
                return;

            // Check if user clicked inside the text box bounds on page 2 (index 1)
            if (this.currentPage == 1)
            {
                if (this.textBoxBounds.Contains(x, y))
                {
                    this.textBoxFocused = true;
                    Game1.keyboardDispatcher.Subscriber = this;
                    this.customTextBox.SetCursorFromClick(x, this.textBoxBounds, this.phoneUiScale);
                    return;
                }
                else
                {
                    this.textBoxFocused = false;
                    if (Game1.keyboardDispatcher.Subscriber == this)
                        Game1.keyboardDispatcher.Subscriber = null;
                }
            }
            else
            {
                this.textBoxFocused = false;
                if (Game1.keyboardDispatcher.Subscriber == this)
                    Game1.keyboardDispatcher.Subscriber = null;
            }

            // Record starting position for swipe/scroll detection.
            this.swipeStartX         = x;
            this.vertScrollStartY    = y;
            this.lastVertScrollMouseY = y;
            this.hasVertScrolled     = false;
            this.isSwiping           = false;
            this.isVertScrolling     = false;
        }

        // -------------------------------------------------------------------------
        // Input – Held
        // -------------------------------------------------------------------------

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            Rectangle frame   = this.FrameRect;
            Rectangle content = this.ContentRect;

            if (!this.isDragging && !this.isSwiping && !this.isVertScrolling)
            {
                if (content.Contains(x, y))
                {
                    int dx = x - this.swipeStartX;
                    int dy = y - this.vertScrollStartY;

                    if (Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dx) > Scale(8))
                        this.isSwiping = true;
                    else if (Math.Abs(dy) > Scale(4))
                        this.isVertScrolling = true;
                }
                else if (frame.Contains(x, y))
                {
                    // Clicking on the bezel starts a drag.
                    this.isDragging  = true;
                    this.dragOffsetX = x - this.xPositionOnScreen;
                    this.dragOffsetY = y - this.yPositionOnScreen;
                }
            }

            if (this.isSwiping)
            {
                int dx = x - this.swipeStartX;
                // Clamp so the user can't drag past the first/last page.
                float rawScroll = -(float)dx / this.contentWidth;
                float minScroll = this.currentPage == 0              ? 0f : -1f;
                float maxScroll = this.currentPage == TotalPages - 1 ? 0f :  1f;
                this.pageScrollX = Math.Clamp(rawScroll, minScroll, maxScroll);
            }

            if (this.isVertScrolling && this.currentPage == 0)
            {
                int delta = y - this.lastVertScrollMouseY;
                this.lastVertScrollMouseY = y;
                this.vertScrollOffset     = Math.Clamp(this.vertScrollOffset - delta, 0, this.maxVertScroll);
                this.hasVertScrolled      = true;
            }
        }

        // -------------------------------------------------------------------------
        // Input – Release
        // -------------------------------------------------------------------------

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);

            if (!this.hasVertScrolled && !this.isSwiping && !this.isDragging)
            {
                if (this.ContentRect.Contains(x, y))
                    this.HandleTap(x, y);
            }

            // When a swipe ends, commit the target page based on how far the user swiped.
            // If the finger moved more than 30% of the page width, flip to the adjacent page;
            // otherwise snap back to the current page.
            if (this.isSwiping)
            {
                const float flipThreshold = 0.30f;
                if (this.pageScrollX > flipThreshold && this.currentPage < TotalPages - 1)
                    this.swipeTargetPage = this.currentPage + 1;
                else if (this.pageScrollX < -flipThreshold && this.currentPage > 0)
                    this.swipeTargetPage = this.currentPage - 1;
                else
                    this.swipeTargetPage = this.currentPage; // bounce back

                this.isSwiping = false;
            }

            this.isDragging      = false;
            this.isVertScrolling = false;
        }

        // -------------------------------------------------------------------------
        // Input – Scroll wheel
        // -------------------------------------------------------------------------

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            if (this.currentPage == 0)
            {
                int amount = Scale(40);
                this.vertScrollOffset = Math.Clamp(
                    this.vertScrollOffset + (direction > 0 ? -amount : amount),
                    0, this.maxVertScroll);
            }
        }

        // -------------------------------------------------------------------------
        // Input – Hover
        // -------------------------------------------------------------------------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.hoveredActionButton = -1;

            if (!this.ContentRect.Contains(x, y)) return;

            if (this.currentPage == 1)
            {
                for (int i = 0; i < this.actionButtons.Count; i++)
                    if (this.actionButtons[i].Rect.Contains(x, y)) { this.hoveredActionButton = i; break; }
            }
        }

        // -------------------------------------------------------------------------
        // Input – Key press
        // -------------------------------------------------------------------------

        public override void receiveKeyPress(Keys key)
        {
            if (this.textBoxFocused)
            {
                if (key == Keys.Escape)
                {
                    this.textBoxFocused = false;
                    if (Game1.keyboardDispatcher.Subscriber == this)
                        Game1.keyboardDispatcher.Subscriber = null;
                    return;
                }

                // Pass the key to the textBox (handles Arrow keys, Delete, Enter, Ctrl+C/V/X etc.)
                this.customTextBox.HandleKeyPress(key);
                return; // Suppress in-game actions
            }

            if (key == Keys.Escape) { this.onBack?.Invoke(); return; }

            // Phone size hotkeys – the key names are configured by the player
            // in the Smartphone settings and exposed via the API.
            string ks = key.ToString();
            if (ks == this.api.GetDecreaseSizeKey()) { this.api.AdjustPhoneSize(-0.1f); return; }
            if (ks == this.api.GetIncreaseSizeKey()) { this.api.AdjustPhoneSize(+0.1f); return; }

            base.receiveKeyPress(key);
        }

        // -------------------------------------------------------------------------
        // Tap handling
        // -------------------------------------------------------------------------

        private void HandleTap(int x, int y)
        {
            if (this.currentPage == 1)
            {
                // Compute the horizontal shift for the current page.
                float totalScrollX = (this.currentPage + this.pageScrollX) * this.contentWidth;
                int shiftX = (int)Math.Round(this.currentPage * this.contentWidth - totalScrollX);

                for (int i = 0; i < this.actionButtons.Count; i++)
                {
                    var (rect, _, _) = this.actionButtons[i];
                    Rectangle draw = new(rect.X + shiftX, rect.Y, rect.Width, rect.Height);

                    if (draw.Contains(x, y))
                    {
                        Game1.playSound("bigSelect");
                        this.ExecuteAction(i);
                        return;
                    }
                }
            }
            else if (this.currentPage == 2)
            {
                // Tap anywhere on page 3 gives the ball a random velocity boost.
                float speed = Scale(4);
                this.ballVelocity = new Vector2(
                    (float)(Game1.random.NextDouble() * 2 - 1) * speed,
                    (float)(Game1.random.NextDouble() * 2 - 1) * speed);
                Game1.playSound("drumkit6");
            }
        }

        // -------------------------------------------------------------------------
        // Page 2 actions
        // -------------------------------------------------------------------------

        private void ExecuteAction(int actionIndex)
        {
            switch (actionIndex)
            {
                case 0: this.Action_SendNotification(); break;
                case 1: this.Action_SelectPhoto();      break;
                case 2: this.Action_DummyAction();      break;
            }
        }

        /// <summary>
        /// Action 0: Send a Smartphone notification.
        /// Uses api.SendSmartphoneNotification(message, notificationName, playerId).
        /// Leaving playerId empty broadcasts to all players; provide the player's
        /// UniqueMultiplayerID to target a specific player.
        /// </summary>
        private void Action_SendNotification()
        {
            this.api.SendSmartphoneNotification(
                message:          "Hello from the Example App! 👋",
                notificationName: "Example App",
                playerId:         Game1.player.UniqueMultiplayerID.ToString());

            this.ShowPopup("📨 Notification sent!\nCheck the Notification app.");
        }

        /// <summary>
        /// Action 1: Open the photo picker to select exactly 1 photo.
        /// Uses api.RetrievePhotos(limit, getTexture, getMetadata, onComplete).
        ///
        /// The photo app will take over as the active menu.  When the user picks
        /// a photo (or cancels), onComplete fires with a JSON array of
        /// SelectedPhotoResult objects.  We then reopen this screen and show the result.
        /// </summary>
        private void Action_SelectPhoto()
        {
            // Capture references needed inside the closure.
            var capturedApi          = this.api;
            var capturedOnBack       = this.onBack;

            this.api.RetrievePhotos(
                limit:       1,
                getTexture:  false,   // set true if you need the raw pixel data
                getMetadata: false,   // set true if you need tag/location/timestamp
                onComplete:  (string json) =>
                {
                    // The callback fires after the photo picker closes.
                    // At this point Game1.activeClickableMenu may be null.
                    // We reopen our screen and pass the result as initialPopup.
                    string popup = ParsePhotoResult(json);

                    Game1.activeClickableMenu = new PortraitAppScreen(
                        api:            capturedApi,
                        onBack:         capturedOnBack,
                        initialPopup:   popup,
                        initialPage:    1);   // return to page 2 (index 1)
                });
        }

        /// <summary>
        /// Action 2: Trigger a dummy action.
        /// </summary>
        private void Action_DummyAction()
        {
            this.ShowPopup("📇 Dummy action triggered! (Contacts event was deprecated)");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void ShowPopup(string message)
        {
            this.popupMessage = message;
            this.popupTimer   = PopupDuration;
        }

        /// <summary>
        /// Parses the JSON returned by RetrievePhotos to extract the selected file name.
        /// The JSON is a serialised List&lt;SelectedPhotoResult&gt;.
        /// We use System.Text.Json.JsonDocument (built into .NET 6) for parsing.
        /// </summary>
        private static string ParsePhotoResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "📷 No photo selected.";

            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    // FileName is populated by the framework for every photo.
                    string fileName = root[0].GetProperty("FileName").GetString() ?? "unknown";
                    return $"📷 Selected:\n{fileName}";
                }

                return "📷 No photo selected.";
            }
            catch
            {
                return "📷 Could not read result.";
            }
        }

        /// <summary>
        /// Wraps long text to fit within maxWidth pixels at the given font scale.
        /// Uses a simple greedy word-wrap algorithm.
        /// </summary>
        private static string WrapText(SpriteFont font, string text, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = new System.Text.StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string[] words = line.Split(' ');
                string current = string.Empty;
                foreach (string word in words)
                {
                    string test = string.IsNullOrEmpty(current) ? word : current + " " + word;
                    float w = font.MeasureString(test).X * scale;
                    if (w > maxWidth && !string.IsNullOrEmpty(current))
                    {
                        result.AppendLine(current);
                        current = word;
                    }
                    else
                    {
                        current = test;
                    }
                }
                if (!string.IsNullOrEmpty(current))
                    result.AppendLine(current);
            }
            return result.ToString().TrimEnd();
        }

        // Note: card background rendering is handled by the shared CardDrawing class.
        // See CardDrawing.cs for the full 9-slice implementation.

        private static Color LightenColor(Color c, float f) =>
            new(Math.Min(255, (int)(c.R + (255 - c.R) * f)),
                Math.Min(255, (int)(c.G + (255 - c.G) * f)),
                Math.Min(255, (int)(c.B + (255 - c.B) * f)));

        // -------------------------------------------------------------------------
        // Keyboard Cleanup
        // -------------------------------------------------------------------------

        protected override void cleanupBeforeExit()
        {
            if (Game1.keyboardDispatcher.Subscriber == this)
                Game1.keyboardDispatcher.Subscriber = null;
            base.cleanupBeforeExit();
        }

        // -------------------------------------------------------------------------
        // IKeyboardSubscriber Implementation
        // -------------------------------------------------------------------------

        public void RecieveTextInput(char inputChar)
        {
            if (!this.textBoxFocused || !this.Selected) return;
            if (!char.IsControl(inputChar))
            {
                this.customTextBox.RecieveTextInput(inputChar.ToString());
            }
        }

        public void RecieveTextInput(string text)
        {
            if (!this.textBoxFocused || !this.Selected) return;
            this.customTextBox.RecieveTextInput(text);
        }

        public void RecieveCommandInput(char command)
        {
            if (!this.textBoxFocused || !this.Selected) return;
            if (command == '\b')
            {
                this.customTextBox.RecieveBackspace();
            }
        }

        public void RecieveSpecialInput(Keys key)
        {
            if (!this.textBoxFocused || !this.Selected) return;
            this.customTextBox.HandleKeyPress(key);
        }
    }
}
