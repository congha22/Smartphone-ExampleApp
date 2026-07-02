// =============================================================================
// LandscapeAppScreen.cs  –  Landscape App Screen
// =============================================================================
// This screen demonstrates a LANDSCAPE phone app (phone rotated -90°).
//
// Key differences from the portrait screen:
//  • The phone frame and background are drawn rotated -90° using XNA's SpriteBatch
//    rotation parameter.
//  • IClickableMenu.width / height are swapped (width = portrait height, etc.).
//  • Click coordinates must be "un-rotated" back to portrait space before
//    being passed to bottom-nav and size-button handlers.
//  • All content is drawn inside a landscape-coordinate scissor rect.
//  • Dragging must track and sync the landscape on-screen position back through
//    api.SetPhonePosition() using the reverse portrait origin calculation.
//
// Everything else (swipe, scroll, scale hotkeys, size buttons) follows the same
// pattern as the portrait screen.
// =============================================================================

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SmartphoneExampleApps.Data;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneExampleApps.LandscapeApp
{
    /// <summary>
    /// A landscape-orientation custom app screen.
    /// The phone is rotated 90° counter-clockwise so it displays wider than tall.
    /// </summary>
    public class LandscapeAppScreen : IClickableMenu
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------

        private readonly ISmartPhoneApi api;
        private readonly Action onBack;

        // -------------------------------------------------------------------------
        // Phone layout  (portrait-space values – updated on scale change)
        // -------------------------------------------------------------------------

        private float phoneUiScale;

        /// <summary>Portrait width = landscape physical height.</summary>
        private int phoneFrameWidth;

        /// <summary>Portrait height = landscape physical width.</summary>
        private int phoneFrameHeight;

        private int phoneContentOffsetX;
        private int phoneContentOffsetY;

        /// <summary>Portrait content width = landscape content height.</summary>
        private int contentWidth;

        /// <summary>Portrait content height = landscape content width.</summary>
        private int contentHeight;

        private Texture2D? phoneFrameTexture;
        private Texture2D? phoneBackgroundTexture;

        // -------------------------------------------------------------------------
        // Landscape screen-space helpers
        //
        // In landscape mode:
        //   Landscape top-left corner = (xPositionOnScreen, yPositionOnScreen)
        //   Landscape physical width  = phoneFrameHeight  (portrait height)
        //   Landscape physical height = phoneFrameWidth   (portrait width)
        //
        //   Content area (landscape):
        //     X = xPositionOnScreen + phoneContentOffsetY
        //     Y = yPositionOnScreen + phoneFrameWidth – phoneContentOffsetX – contentWidth
        //     W = contentHeight   (portrait content height)
        //     H = contentWidth    (portrait content width)
        // -------------------------------------------------------------------------

        /// <summary>Landscape frame rectangle (the whole phone including bezel).</summary>
        private Rectangle LandscapeFrameRect =>
            new(this.xPositionOnScreen, this.yPositionOnScreen,
                this.phoneFrameHeight, this.phoneFrameWidth);

        /// <summary>Landscape content rectangle (the usable screen area).</summary>
        private Rectangle LandscapeContentRect =>
            new(this.xPositionOnScreen + this.phoneContentOffsetY,
                this.yPositionOnScreen + this.phoneFrameWidth - this.phoneContentOffsetX - this.contentWidth,
                this.contentHeight,   // landscape width  = portrait content height
                this.contentWidth);   // landscape height = portrait content width

        // -------------------------------------------------------------------------
        // Drag state
        // -------------------------------------------------------------------------

        private bool isDragging;
        private int  dragOffsetX;
        private int  dragOffsetY;

        // -------------------------------------------------------------------------
        // Horizontal swipe state
        // -------------------------------------------------------------------------

        private int   currentPage = 0;
        private const int TotalPages = 3;
        private float pageScrollX  = 0f;
        private bool  isSwiping;
        private int   swipeStartX;
        private float swipeVelocity = 0f;

        // -------------------------------------------------------------------------
        // Vertical scroll state  (page 0)
        // -------------------------------------------------------------------------

        private int  vertScrollOffset = 0;
        private int  maxVertScroll    = 0;
        private bool isVertScrolling;
        private int  vertScrollStartY;
        private int  lastVertScrollMouseY;
        private bool hasVertScrolled;

        // -------------------------------------------------------------------------
        // Animation timer
        // -------------------------------------------------------------------------

        private float bounceTimer = 0f;

        // -------------------------------------------------------------------------
        // Page 1 – Cards
        // -------------------------------------------------------------------------

        private readonly List<(Rectangle Rect, string Label, Color Color)> cards = new();
        private int hoveredCard = -1;

        // -------------------------------------------------------------------------
        // Page 2 – Buttons
        // -------------------------------------------------------------------------

        private readonly List<(Rectangle Rect, string Label, Color Color)> btns = new();
        private int hoveredBtn = -1;

        // -------------------------------------------------------------------------
        // Page 3 – Bouncing ball
        // -------------------------------------------------------------------------

        private Vector2 ballPos;
        private Vector2 ballVel;
        private float   ballRadius;

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------

        public LandscapeAppScreen(ISmartPhoneApi api, Action onBack) : base()
        {
            this.api    = api;
            this.onBack = onBack;

            // Read portrait position from the framework, then compute the landscape
            // on-screen top-left corner by rotating around the phone centre.
            var (px, py) = api.GetPhonePosition();

            this.phoneFrameWidth  = api.GetPhoneFrameWidth();
            this.phoneFrameHeight = api.GetPhoneFrameHeight();

            // Centre-rotate: portrait → landscape offset
            // Portrait centre = (px + W/2, py + H/2)
            // Landscape TL    = centre – (H/2, W/2)
            this.xPositionOnScreen = px + (this.phoneFrameWidth  - this.phoneFrameHeight) / 2;
            this.yPositionOnScreen = py + (this.phoneFrameHeight - this.phoneFrameWidth)  / 2;

            this.RefreshLayout();
            this.ResetBall();
        }

        // -------------------------------------------------------------------------
        // Layout helpers
        // -------------------------------------------------------------------------

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

            // In landscape the IClickableMenu width/height are swapped.
            this.width  = this.phoneFrameHeight;  // landscape physical width
            this.height = this.phoneFrameWidth;   // landscape physical height

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

            this.RebuildCards();
            this.RebuildButtons();
        }

        private int Scale(int v) => (int)Math.Round(v * this.phoneUiScale);

        // -------------------------------------------------------------------------
        // Page 1 – Cards (landscape-space positions)
        // -------------------------------------------------------------------------

        private void RebuildCards()
        {
            this.cards.Clear();
            Rectangle lc = this.LandscapeContentRect;

            int cardH  = Scale(60);
            int cardW  = lc.Width - Scale(24);
            int gap    = Scale(8);
            int startX = lc.X + Scale(12);

            var palette = new[]
            {
                new Color(70, 130, 220), new Color(80, 190, 120),
                new Color(210, 100,  80), new Color(160,  90, 210),
                new Color(210, 160,  40),
            };
            string[] labels = { "Landscape Card A", "Landscape Card B", "Landscape Card C",
                                 "Landscape Card D", "Landscape Card E" };

            for (int i = 0; i < labels.Length; i++)
            {
                int y = lc.Y + Scale(10) + i * (cardH + gap);
                this.cards.Add((new Rectangle(startX, y, cardW, cardH), labels[i], palette[i % palette.Length]));
            }

            int totalH = labels.Length * (cardH + gap) + Scale(20);
            this.maxVertScroll = Math.Max(0, totalH - lc.Height);
        }

        // -------------------------------------------------------------------------
        // Page 2 – Buttons (landscape-space positions)
        // -------------------------------------------------------------------------

        private void RebuildButtons()
        {
            this.btns.Clear();
            Rectangle lc = this.LandscapeContentRect;

            int cols = 3;
            int rows = 2;
            int padX = Scale(12);
            int padY = Scale(30);
            int gapX = Scale(10);
            int gapY = Scale(10);
            int btnW = (lc.Width - padX * 2 - gapX * (cols - 1)) / cols;
            int btnH = Scale(55);

            var colors = new[]
            {
                new Color(40, 120, 220), new Color(220, 80, 80),
                new Color(60, 180, 100), new Color(200, 160, 40),
                new Color(150, 60, 200), new Color(60, 180, 200),
            };
            string[] labels = { "Action 1", "Action 2", "Action 3", "Action 4", "Action 5", "Action 6" };

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    if (idx >= labels.Length) break;
                    int bx = lc.X + padX + c * (btnW + gapX);
                    int by = lc.Y + padY + r * (btnH + gapY);
                    this.btns.Add((new Rectangle(bx, by, btnW, btnH), labels[idx], colors[idx]));
                }
        }

        // -------------------------------------------------------------------------
        // Page 3 – Bouncing ball
        // -------------------------------------------------------------------------

        private void ResetBall()
        {
            Rectangle lc  = this.LandscapeContentRect;
            this.ballRadius = Scale(16);
            this.ballPos    = new Vector2(lc.Center.X, lc.Center.Y);
            this.ballVel    = new Vector2(Scale(3), Scale(2));
        }

        // -------------------------------------------------------------------------
        // Update
        // -------------------------------------------------------------------------

        public override void update(GameTime time)
        {
            // Check for scale change
            float cur = this.api.GetPhoneUiScale();
            if (Math.Abs(cur - this.phoneUiScale) > 0.001f)
            {
                // Capture centre of the landscape phone before resizing
                int oldLW = this.phoneFrameHeight;
                int oldLH = this.phoneFrameWidth;
                int cx = this.xPositionOnScreen + oldLW / 2;
                int cy = this.yPositionOnScreen + oldLH / 2;

                this.phoneUiScale = cur;
                this.RefreshLayout();

                // Re-centre the landscape frame around the same screen point
                this.xPositionOnScreen = cx - this.phoneFrameHeight / 2;
                this.yPositionOnScreen = cy - this.phoneFrameWidth  / 2;

                // Sync the portrait origin back to the framework
                this.SyncPortraitPosition();
                this.ResetBall();
            }

            base.update(time);
            float dt = (float)time.ElapsedGameTime.TotalSeconds;
            this.bounceTimer += dt;

            // Drag
            if (this.isDragging)
            {
                this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
                this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
                this.RebuildCards();
                this.RebuildButtons();
                this.SyncPortraitPosition();
            }

            // Swipe inertia
            if (Math.Abs(this.swipeVelocity) > 0.5f)
            {
                this.pageScrollX   += this.swipeVelocity * dt * 3f;
                this.swipeVelocity *= 1f - (10f * dt);
            }

            if (!this.isSwiping && Math.Abs(this.swipeVelocity) < 0.5f)
            {
                float raw = this.currentPage + this.pageScrollX;
                int target = Math.Clamp((int)Math.Round(raw), 0, TotalPages - 1);
                this.currentPage  = target;
                this.pageScrollX  = 0f;
                this.swipeVelocity = 0f;
            }

            // Bounce ball on page 3
            if (this.currentPage == 2)
            {
                Rectangle lc = this.LandscapeContentRect;
                this.ballPos += this.ballVel;

                if (this.ballPos.X - this.ballRadius < lc.Left)
                {
                    this.ballPos.X = lc.Left + this.ballRadius;
                    this.ballVel.X = Math.Abs(this.ballVel.X);
                    Game1.playSound("drumkit6");
                }
                if (this.ballPos.X + this.ballRadius > lc.Right)
                {
                    this.ballPos.X = lc.Right - this.ballRadius;
                    this.ballVel.X = -Math.Abs(this.ballVel.X);
                    Game1.playSound("drumkit6");
                }
                if (this.ballPos.Y - this.ballRadius < lc.Top)
                {
                    this.ballPos.Y = lc.Top + this.ballRadius;
                    this.ballVel.Y = Math.Abs(this.ballVel.Y);
                    Game1.playSound("drumkit6");
                }
                if (this.ballPos.Y + this.ballRadius > lc.Bottom)
                {
                    this.ballPos.Y = lc.Bottom - this.ballRadius;
                    this.ballVel.Y = -Math.Abs(this.ballVel.Y);
                    Game1.playSound("drumkit6");
                }
            }
        }

        /// <summary>
        /// Converts the current landscape top-left position back to the portrait
        /// coordinate origin and saves it in the framework via SetPhonePosition.
        ///
        /// Formula (inverse of the constructor):
        ///   px = landX – (W – H) / 2
        ///   py = landY – (H – W) / 2
        /// </summary>
        private void SyncPortraitPosition()
        {
            int px = this.xPositionOnScreen - (this.phoneFrameWidth  - this.phoneFrameHeight) / 2;
            int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth)  / 2;
            this.api.SetPhonePosition(px, py);
        }

        // -------------------------------------------------------------------------
        // Draw
        // -------------------------------------------------------------------------

        public override void draw(SpriteBatch b)
        {
            // 1. Dim the world
            b.Draw(Game1.staminaRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.55f);

            Rectangle lc = this.LandscapeContentRect;
            int lx = lc.X, ly = lc.Y, lw = lc.Width, lh = lc.Height;

            // 2. Draw phone background rotated -90° in landscape space.
            //    We draw from the bottom-left corner of the landscape content area
            //    and rotate -PiOver2.
            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
            {
                float sx = (float)this.contentWidth  / this.phoneBackgroundTexture.Width;
                float sy = (float)this.contentHeight / this.phoneBackgroundTexture.Height;
                b.Draw(
                    this.phoneBackgroundTexture,
                    new Vector2(lx, ly + lh),   // bottom-left of landscape content
                    null,
                    Color.White,
                    -MathHelper.PiOver2,         // rotate -90°
                    Vector2.Zero,
                    new Vector2(sx, sy),
                    SpriteEffects.None,
                    0f);
            }
            else
            {
                b.Draw(Game1.staminaRect, new Rectangle(lx, ly, lw, lh), new Color(20, 24, 36));
            }

            // 3. Scissor-clipped content drawing
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    null, new RasterizerState { ScissorTestEnable = true });

            Rectangle prevScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle =
                Rectangle.Intersect(lc, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            this.DrawPages(b, lc);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = prevScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // 4. Page dots (drawn below the landscape content area)
            this.DrawPageDots(b, lc);

            // 5. Draw the phone bezel frame rotated -90°.
            if (this.phoneFrameTexture != null && !this.phoneFrameTexture.IsDisposed)
            {
                float sx = (float)this.phoneFrameWidth  / this.phoneFrameTexture.Width;
                float sy = (float)this.phoneFrameHeight / this.phoneFrameTexture.Height;
                b.Draw(
                    this.phoneFrameTexture,
                    new Vector2(this.xPositionOnScreen, this.yPositionOnScreen + this.phoneFrameWidth),
                    null,
                    Color.White,
                    -MathHelper.PiOver2,
                    Vector2.Zero,
                    new Vector2(sx, sy),
                    SpriteEffects.None,
                    0f);
            }

            // 6. Draw size-adjustment buttons.
            //    Pass landscape: true so the framework positions them on the rotated bezel.
            this.api.DrawPhoneSizeButtons(
                b, this.xPositionOnScreen, this.yPositionOnScreen, landscape: true);

            drawMouse(b);
        }

        // -------------------------------------------------------------------------
        // Page rendering
        // -------------------------------------------------------------------------

        private void DrawPages(SpriteBatch b, Rectangle lc)
        {
            float totalScrollX = (this.currentPage + this.pageScrollX) * lc.Width;

            for (int p = 0; p < TotalPages; p++)
            {
                int pageOffX = (int)Math.Round(p * lc.Width - totalScrollX);
                Rectangle pageRect = new(lc.X + pageOffX, lc.Y, lc.Width, lc.Height);

                if (pageRect.Right < lc.Left - 10 || pageRect.Left > lc.Right + 10)
                    continue;

                switch (p)
                {
                    case 0: DrawPage1_Cards(b, pageRect, lc); break;
                    case 1: DrawPage2_Buttons(b, pageRect, lc); break;
                    case 2: DrawPage3_Ball(b, pageRect, lc); break;
                }
            }
        }

        private void DrawPage1_Cards(SpriteBatch b, Rectangle pageRect, Rectangle lc)
        {
            SpriteFont font = Game1.dialogueFont;
            int shiftX = pageRect.X - lc.X;

            string header = "Page 1 – Scroll!";
            float hs = 0.48f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(6) - this.vertScrollOffset),
                Color.White * 0.85f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            for (int i = 0; i < this.cards.Count; i++)
            {
                var (rect, label, color) = this.cards[i];
                Rectangle draw = new(rect.X + shiftX, rect.Y - this.vertScrollOffset, rect.Width, rect.Height);
                bool hovered   = i == this.hoveredCard;
                Color fill     = hovered ? LightenColor(color, 0.20f) : color;

                // Use the shared CardDrawing utility
                CardDrawing.DrawCard(b, draw.X, draw.Y, draw.Width, draw.Height, fill, scale: 1f, drawShadow: hovered);

                // Draw centered label text manually
                float ts = 0.42f * this.phoneUiScale;
                Vector2 sz = font.MeasureString(label) * ts;
                Vector2 pos = new(draw.Center.X - sz.X / 2f, draw.Center.Y - sz.Y / 2f);
                b.DrawString(font, label, pos + new Vector2(1f, 1f), Color.Black * 0.30f, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
                b.DrawString(font, label, pos, Color.White, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
            }
        }

        private void DrawPage2_Buttons(SpriteBatch b, Rectangle pageRect, Rectangle lc)
        {
            SpriteFont font = Game1.dialogueFont;
            int shiftX = pageRect.X - lc.X;

            string header = "Page 2 – Buttons!";
            float hs = 0.48f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(8)),
                Color.White * 0.85f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            for (int i = 0; i < this.btns.Count; i++)
            {
                var (rect, label, color) = this.btns[i];
                Rectangle draw = new(rect.X + shiftX, rect.Y, rect.Width, rect.Height);
                bool hovered   = i == this.hoveredBtn;
                Color fill     = hovered ? LightenColor(color, 0.20f) : color;

                // Use the shared CardDrawing utility
                CardDrawing.DrawCard(b, draw.X, draw.Y, draw.Width, draw.Height, fill, scale: 1f, drawShadow: hovered);

                // Draw centered label text manually
                float ts = 0.42f * this.phoneUiScale;
                Vector2 sz = font.MeasureString(label) * ts;
                Vector2 pos = new(draw.Center.X - sz.X / 2f, draw.Center.Y - sz.Y / 2f);
                b.DrawString(font, label, pos + new Vector2(1f, 1f), Color.Black * 0.30f, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
                b.DrawString(font, label, pos, Color.White, 0f, Vector2.Zero, ts, SpriteEffects.None, 1f);
            }
        }

        private void DrawPage3_Ball(SpriteBatch b, Rectangle pageRect, Rectangle lc)
        {
            SpriteFont font = Game1.dialogueFont;
            int shiftX = pageRect.X - lc.X;

            b.Draw(Game1.staminaRect, pageRect, new Color(10, 15, 30) * 0.7f);

            string header = "Page 3 – Bounce!";
            float hs = 0.45f * this.phoneUiScale;
            Vector2 hSz = font.MeasureString(header) * hs;
            b.DrawString(font, header,
                new Vector2(pageRect.Center.X - hSz.X / 2f, pageRect.Y + Scale(6)),
                Color.Cyan * 0.9f, 0f, Vector2.Zero, hs, SpriteEffects.None, 1f);

            int r = (int)this.ballRadius;
            Color ball = new(
                (int)(Math.Sin(this.bounceTimer * 2f) * 127 + 128),
                (int)(Math.Sin(this.bounceTimer * 3f + 2f) * 127 + 128),
                (int)(Math.Sin(this.bounceTimer * 5f + 4f) * 127 + 128));

            b.Draw(Game1.staminaRect,
                new Rectangle((int)(this.ballPos.X + shiftX - r), (int)(this.ballPos.Y - r), r * 2, r * 2),
                ball);
            b.Draw(Game1.staminaRect,
                new Rectangle((int)(this.ballPos.X + shiftX - r / 2), (int)(this.ballPos.Y - r + 4), r / 2, r / 2),
                Color.White * 0.4f);
        }

        // -------------------------------------------------------------------------
        // Page dots
        // -------------------------------------------------------------------------

        private void DrawPageDots(SpriteBatch b, Rectangle lc)
        {
            int dotSize   = Scale(7);
            int dotGap    = Scale(5);
            int totalDotW = TotalPages * dotSize + (TotalPages - 1) * dotGap;
            int dotY      = lc.Bottom + Scale(5);
            int dotStartX = lc.Center.X - totalDotW / 2;

            for (int i = 0; i < TotalPages; i++)
            {
                Color col = i == this.currentPage ? Color.White : Color.Gray;
                b.Draw(Game1.staminaRect,
                    new Rectangle(dotStartX + i * (dotSize + dotGap), dotY, dotSize, dotSize), col);
            }
        }

        // -------------------------------------------------------------------------
        // Input: Left click
        // -------------------------------------------------------------------------

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // Bottom nav and size buttons expect portrait-space coordinates.
            // Convert the landscape click position back to portrait space.
            int px, py;
            LandscapeToPortraitClick(x, y, out px, out py);

            int portraitOriginX = this.xPositionOnScreen - (this.phoneFrameWidth  - this.phoneFrameHeight) / 2;
            int portraitOriginY = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth)  / 2;

            if (this.api.HandlePhoneAppBottomNavClick(px, py, portraitOriginX, portraitOriginY, onBack: this.onBack))
                return;

            if (this.api.HandlePhoneSizeButtonsClick(px, py, portraitOriginX, portraitOriginY))
                return;

            this.swipeStartX       = x;
            this.vertScrollStartY  = y;
            this.lastVertScrollMouseY = y;
            this.hasVertScrolled   = false;
            this.isSwiping         = false;
            this.isVertScrolling   = false;
        }

        // -------------------------------------------------------------------------
        // Input: Held
        // -------------------------------------------------------------------------

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            Rectangle lFrame   = this.LandscapeFrameRect;
            Rectangle lContent = this.LandscapeContentRect;

            if (!this.isDragging && !this.isSwiping && !this.isVertScrolling)
            {
                if (lContent.Contains(x, y))
                {
                    int dx = x - this.swipeStartX;
                    int dy = y - this.vertScrollStartY;

                    if (Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dx) > Scale(8))
                        this.isSwiping = true;
                    else if (Math.Abs(dy) > Scale(4))
                        this.isVertScrolling = true;
                }
                else if (lFrame.Contains(x, y))
                {
                    this.isDragging  = true;
                    this.dragOffsetX = x - this.xPositionOnScreen;
                    this.dragOffsetY = y - this.yPositionOnScreen;
                }
            }

            if (this.isSwiping)
            {
                int dx = x - this.swipeStartX;
                this.pageScrollX   = -(float)dx / this.LandscapeContentRect.Width;
                this.swipeVelocity = -(float)dx / this.LandscapeContentRect.Width;
            }

            if (this.isVertScrolling && this.currentPage == 0)
            {
                int delta = y - this.lastVertScrollMouseY;
                this.lastVertScrollMouseY = y;
                this.vertScrollOffset = Math.Clamp(this.vertScrollOffset - delta, 0, this.maxVertScroll);
                this.hasVertScrolled  = true;
            }
        }

        // -------------------------------------------------------------------------
        // Input: Release
        // -------------------------------------------------------------------------

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);

            if (!this.hasVertScrolled && !this.isSwiping && !this.isDragging)
            {
                if (this.LandscapeContentRect.Contains(x, y))
                    this.HandleTap(x, y);
            }

            this.isDragging      = false;
            this.isVertScrolling = false;
            if (this.isSwiping) this.isSwiping = false;
        }

        // -------------------------------------------------------------------------
        // Input: Scroll wheel
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
        // Input: Hover
        // -------------------------------------------------------------------------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.hoveredCard = -1;
            this.hoveredBtn  = -1;

            if (!this.LandscapeContentRect.Contains(x, y)) return;

            if (this.currentPage == 0)
            {
                for (int i = 0; i < this.cards.Count; i++)
                {
                    var (rect, _, _) = this.cards[i];
                    Rectangle adjusted = new(rect.X, rect.Y - this.vertScrollOffset, rect.Width, rect.Height);
                    if (adjusted.Contains(x, y)) { this.hoveredCard = i; break; }
                }
            }
            else if (this.currentPage == 1)
            {
                for (int i = 0; i < this.btns.Count; i++)
                    if (this.btns[i].Rect.Contains(x, y)) { this.hoveredBtn = i; break; }
            }
        }

        // -------------------------------------------------------------------------
        // Input: Key press
        // -------------------------------------------------------------------------

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape) { this.onBack?.Invoke(); return; }

            string ks = key.ToString();
            if (ks == this.api.GetDecreaseSizeKey()) { this.api.AdjustPhoneSize(-0.1f); return; }
            if (ks == this.api.GetIncreaseSizeKey()) { this.api.AdjustPhoneSize(0.1f);  return; }

            base.receiveKeyPress(key);
        }

        // -------------------------------------------------------------------------
        // Tap
        // -------------------------------------------------------------------------

        private void HandleTap(int x, int y)
        {
            if (this.currentPage == 0)
            {
                foreach (var (rect, label, _) in this.cards)
                {
                    if (rect.Contains(x, y))
                    {
                        Game1.playSound("coin");
                        return;
                    }
                }
            }
            else if (this.currentPage == 1)
            {
                foreach (var (rect, label, _) in this.btns)
                    if (rect.Contains(x, y)) { Game1.playSound("bigSelect"); return; }
            }
            else if (this.currentPage == 2)
            {
                float speed = Scale(3);
                this.ballVel = new Vector2(
                    (float)(Game1.random.NextDouble() * 2 - 1) * speed,
                    (float)(Game1.random.NextDouble() * 2 - 1) * speed);
                Game1.playSound("drumkit6");
            }
        }

        // -------------------------------------------------------------------------
        // Coordinate conversion
        // -------------------------------------------------------------------------

        /// <summary>
        /// Converts a screen-space click at (x, y) from landscape coordinates back
        /// to portrait coordinates, relative to the portrait phone origin.
        ///
        /// The landscape phone is the portrait phone rotated -90° around its centre:
        ///   Portrait origin = (landX – (W – H)/2, landY – (H – W)/2)
        ///
        /// A click at landscape (cx, cy) maps to portrait (px, py) via:
        ///   px = portraitOriginX + (landY + landW – cy)
        ///   py = portraitOriginY + (cx – landX)
        ///
        /// where landX/landY is the landscape on-screen top-left and landW = phoneFrameHeight.
        /// </summary>
        private void LandscapeToPortraitClick(int cx, int cy, out int px, out int py)
        {
            int portraitOriginX = this.xPositionOnScreen - (this.phoneFrameWidth  - this.phoneFrameHeight) / 2;
            int portraitOriginY = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth)  / 2;
            px = portraitOriginX + (this.yPositionOnScreen + this.phoneFrameWidth - cy);
            py = portraitOriginY + (cx - this.xPositionOnScreen);
        }

        // Note: card background rendering is handled by the shared CardDrawing class.
        // See CardDrawing.cs for the full 9-slice implementation.

        private static Color LightenColor(Color c, float f) =>
            new(Math.Min(255, (int)(c.R + (255 - c.R) * f)),
                Math.Min(255, (int)(c.G + (255 - c.G) * f)),
                Math.Min(255, (int)(c.B + (255 - c.B) * f)));
    }
}
