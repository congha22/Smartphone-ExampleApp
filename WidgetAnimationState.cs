// =============================================================================
// WidgetAnimationState.cs  –  4x2 Widget Particle Animation
// =============================================================================
// This class manages the "floating letters" particle animation drawn on the
// right half of the 4x2 widget. Letters spawn at the bottom, float upward,
// and fade out before being removed.
//
// Usage:
//   - Create one instance per app (shared across both theme variants).
//   - Call Update(gameTime) from ModEntry's GameLoop.UpdateTicked event.
//   - Call Draw(spriteBatch, rightHalfRect) inside the onDrawWidget callback
//     when the AppSize is Size4x2.
// =============================================================================

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace SmartphoneExampleApps
{
    /// <summary>
    /// Holds state for a single floating-letter particle in the 4x2 widget animation.
    /// </summary>
    internal class LetterParticle
    {
        /// <summary>The letter character to display.</summary>
        public char Letter;

        /// <summary>
        /// Normalized position within the animation area (0..1 for both axes).
        /// X=0 is left edge of the right-half area, Y=0 is top, Y=1 is bottom.
        /// </summary>
        public float NormX;
        public float NormY;

        /// <summary>Current opacity (1 = fully visible, 0 = invisible and ready to remove).</summary>
        public float Alpha;

        /// <summary>Upward speed per second, as a fraction of the area height.</summary>
        public float Speed;

        /// <summary>The colour of this letter (randomly chosen at spawn).</summary>
        public Color Color;

        /// <summary>Scale of the letter relative to the base font scale.</summary>
        public float Scale;
    }

    /// <summary>
    /// Manages spawning, updating, and drawing the floating-letter particles that
    /// appear on the right half of the 4x2 widget.
    /// </summary>
    internal class WidgetAnimationState
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        /// <summary>Maximum number of live particles at any time.</summary>
        private const int MaxParticles = 18;

        /// <summary>How many seconds between each new particle spawn.</summary>
        private const float SpawnInterval = 0.18f;

        /// <summary>Letters that can be spawned (alphanumeric + some symbols).</summary>
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!?#@*";

        /// <summary>Palette of vibrant colours for the letters.</summary>
        private static readonly Color[] Palette =
        {
            new Color(255, 100, 100),   // coral red
            new Color(100, 200, 255),   // sky blue
            new Color(140, 255, 100),   // lime green
            new Color(255, 220,  60),   // golden yellow
            new Color(200, 120, 255),   // violet
            new Color(255, 165,  60),   // orange
            new Color( 60, 220, 200),   // teal
            new Color(255, 255, 255),   // white
        };

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private readonly List<LetterParticle> particles = new();
        private float spawnTimer = 0f;
        private readonly Random rng = new();

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Update particle positions and spawn new ones.
        /// Call this every game tick from GameLoop.UpdateTicked.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // --- Update existing particles ---
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];

                // Move upward
                p.NormY -= p.Speed * dt;

                // Fade out in the top 30% of the area
                if (p.NormY < 0.3f)
                    p.Alpha -= dt * 1.8f;

                // Remove invisible or off-screen particles
                if (p.Alpha <= 0f || p.NormY < -0.05f)
                    particles.RemoveAt(i);
            }

            // --- Spawn new particles ---
            spawnTimer -= dt;
            if (spawnTimer <= 0f && particles.Count < MaxParticles)
            {
                SpawnParticle();
                spawnTimer = SpawnInterval + (float)rng.NextDouble() * 0.1f;
            }
        }

        /// <summary>
        /// Draw all visible particles inside the given destination rectangle.
        /// </summary>
        /// <param name="b">The active SpriteBatch.</param>
        /// <param name="area">The screen rectangle to draw particles into.</param>
        public void Draw(SpriteBatch b, Rectangle area)
        {
            SpriteFont font = Game1.dialogueFont;

            foreach (var p in particles)
            {
                if (p.Alpha <= 0f)
                    continue;

                // Convert normalised coordinates to screen pixels
                float screenX = area.X + p.NormX * area.Width;
                float screenY = area.Y + p.NormY * area.Height;

                // The base font scale is kept small so letters fit in a widget cell
                float fontScale = p.Scale * (area.Height / 120f); // 120 px reference height
                fontScale = Math.Max(0.1f, fontScale);

                string text = p.Letter.ToString();
                Vector2 size = font.MeasureString(text) * fontScale;

                // Draw drop shadow first, then the coloured letter on top
                Color letterColor = p.Color * p.Alpha;
                Color shadowColor = Color.Black * (p.Alpha * 0.4f);

                b.DrawString(
                    font,
                    text,
                    new Vector2(screenX - size.X / 2f + 1f, screenY - size.Y / 2f + 1f),
                    shadowColor,
                    0f, Vector2.Zero, fontScale, SpriteEffects.None, 1f);

                b.DrawString(
                    font,
                    text,
                    new Vector2(screenX - size.X / 2f, screenY - size.Y / 2f),
                    letterColor,
                    0f, Vector2.Zero, fontScale, SpriteEffects.None, 1f);
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private void SpawnParticle()
        {
            particles.Add(new LetterParticle
            {
                Letter = Alphabet[rng.Next(Alphabet.Length)],
                NormX  = (float)rng.NextDouble(),      // random horizontal position
                NormY  = 0.95f + (float)rng.NextDouble() * 0.1f, // spawn near bottom
                Alpha  = 0.85f + (float)rng.NextDouble() * 0.15f,
                Speed  = 0.18f + (float)rng.NextDouble() * 0.22f, // varied upward speed
                Color  = Palette[rng.Next(Palette.Length)],
                Scale  = 0.35f + (float)rng.NextDouble() * 0.25f,
            });
        }
    }
}
