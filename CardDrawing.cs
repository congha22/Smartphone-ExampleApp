// =============================================================================
// CardDrawing.cs  –  Shared 9-slice Card Drawing Utility
// =============================================================================
// Drop-in equivalent of the CardDrawing helper from Smartphone-AppMessenger.
//
// Usage (from any screen or class in this mod):
//
//   // Simple overload – just bounds and colour:
//   CardDrawing.DrawCard(b, myRect, Color.CornflowerBlue);
//
//   // Full overload – control scale, shadow, and layer depth:
//   CardDrawing.DrawCard(b, x, y, width, height, color,
//                        scale: 1f, drawShadow: true, draw_layer: 0.8f);
//
// The method fetches the card texture from the Smartphone API automatically.
// If the API or texture is unavailable it falls back to the standard Stardew
// Valley menu box texture so the UI always renders correctly.
//
// Note: ModEntry.Api must be set before any call to DrawCard.
// =============================================================================

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace SmartphoneExampleApps
{
    public static class CardDrawing
    {
        // -------------------------------------------------------------------------
        // Full overload – mirrors CardDrawing in Smartphone exactly
        // -------------------------------------------------------------------------

        /// <summary>
        /// Draws a 9-slice card background using the Smartphone framework's card
        /// texture, tinted with <paramref name="color"/>.
        ///
        /// The card texture is a square sprite sheet split into a 3×3 grid of equal
        /// cells.  Corners are drawn at 1:1 pixel scale; edges and the centre fill
        /// are stretched to fit the requested <paramref name="width"/> ×
        /// <paramref name="height"/>.
        ///
        /// Falls back to Game1.menuTexture (source rect 0,256,60,60) if the API
        /// card texture is not available.
        /// </summary>
        /// <param name="b">Active SpriteBatch.</param>
        /// <param name="x">Left edge of the card (screen pixels).</param>
        /// <param name="y">Top edge of the card (screen pixels).</param>
        /// <param name="width">Total card width in screen pixels.</param>
        /// <param name="height">Total card height in screen pixels.</param>
        /// <param name="color">Tint colour applied to the card texture.</param>
        /// <param name="scale">Uniform scale applied to the corner pieces (default 1).</param>
        /// <param name="drawShadow">When true, draws a soft drop-shadow behind the card.</param>
        /// <param name="draw_layer">SpriteBatch sort layer. Negative = auto-calculated.</param>
        public static void DrawCard(SpriteBatch b, int x, int y, int width, int height,
            Color color, float scale = 1f, bool drawShadow = false, float draw_layer = -1f)
        {
            // Resolve the card texture from the API, with a graceful fallback.
            Texture2D texture = ModEntry.Api?.GetCardTexture() ?? Game1.menuTexture;
            Rectangle sourceRect = ModEntry.Api?.GetCardTexture() != null
                ? texture.Bounds
                : new Rectangle(0, 256, 60, 60);

            // Each of the 9 slices occupies 1/3 of the source rect width.
            int num = sourceRect.Width / 3;

            // Sort-layer calculation (matches AppMessenger's convention).
            float layerDepth = draw_layer - 0.03f;
            if (draw_layer < 0f)
            {
                draw_layer = 0.8f - (float)y * 1E-06f;
                layerDepth = 0.77f;
            }

            // ------------------------------------------------------------------
            // Optional drop shadow
            // Drawn before the card body so it appears behind it.
            // ------------------------------------------------------------------
            if (drawShadow)
            {
                Color shadowColor = Color.Black * 0.4f;

                // Shadow corners (only the three visible ones for a bottom-right shadow)
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + 8),
                    new Rectangle(sourceRect.X + num * 2, sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x - 8, y + height - (int)((float)num * scale) + 8),
                    new Rectangle(sourceRect.X, num * 2 + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8),
                    new Rectangle(sourceRect.X + num * 2, num * 2 + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);

                // Shadow edges
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + 8, width - (int)((float)num * scale) * 2, (int)((float)num * scale)),
                    new Rectangle(sourceRect.X + num, sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8, width - (int)((float)num * scale) * 2, (int)((float)num * scale)),
                    new Rectangle(sourceRect.X + num, num * 2 + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - (int)((float)num * scale) * 2),
                    new Rectangle(sourceRect.X, num + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale) - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - (int)((float)num * scale) * 2),
                    new Rectangle(sourceRect.X + num * 2, num + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);

                // Shadow centre fill
                b.Draw(texture, new Rectangle((int)((float)num * scale / 2f) + x - 8, (int)((float)num * scale / 2f) + y + 8, width - (int)((float)num * scale), height - (int)((float)num * scale)),
                    new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num),
                    shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
            }

            // ------------------------------------------------------------------
            // Card body – 9 pieces
            // ------------------------------------------------------------------

            // Centre fill (stretched)
            b.Draw(texture,
                new Rectangle((int)((float)num * scale) + x, (int)((float)num * scale) + y,
                              width - (int)((float)num * scale * 2f), height - (int)((float)num * scale * 2f)),
                new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);

            // Four corners (1:1 scale)
            b.Draw(texture, new Vector2(x, y),
                new Rectangle(sourceRect.X, sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y),
                new Rectangle(sourceRect.X + num * 2, sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x, y + height - (int)((float)num * scale)),
                new Rectangle(sourceRect.X, num * 2 + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y + height - (int)((float)num * scale)),
                new Rectangle(sourceRect.X + num * 2, num * 2 + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);

            // Four edges (stretched)
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y, width - (int)((float)num * scale) * 2, (int)((float)num * scale)),
                new Rectangle(sourceRect.X + num, sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y + height - (int)((float)num * scale), width - (int)((float)num * scale) * 2, (int)((float)num * scale)),
                new Rectangle(sourceRect.X + num, num * 2 + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x, y + (int)((float)num * scale), (int)((float)num * scale), height - (int)((float)num * scale) * 2),
                new Rectangle(sourceRect.X, num + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale), y + (int)((float)num * scale), (int)((float)num * scale), height - (int)((float)num * scale) * 2),
                new Rectangle(sourceRect.X + num * 2, num + sourceRect.Y, num, num),
                color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
        }

        // -------------------------------------------------------------------------
        // Convenience overload – just bounds and colour
        // -------------------------------------------------------------------------

        /// <summary>
        /// Convenience overload: draws a card at the given <paramref name="bounds"/>
        /// with default scale (1), no shadow, and auto layer depth.
        /// </summary>
        public static void DrawCard(SpriteBatch b, Rectangle bounds, Color color)
        {
            DrawCard(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, color,
                     scale: 1f, drawShadow: false, draw_layer: -1f);
        }
    }
}
