using System;

using Microsoft.Xna.Framework;

namespace GameProgrammingI_FinalProject
{
    /// <summary>
    /// A 2D camera that smoothly follows a point and never shows anything outside the map.
    /// World-space drawing goes through Transform; screen-space things (the HUD) ignore it.
    /// </summary>
    public class Camera
    {
        private const float FollowSpeed = 8f;       // higher = snappier

        public Point ViewSize { get; set; }
        public Rectangle WorldBounds { get; set; }

        /// <summary>The world point at the center of the screen.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>World position of the screen's top-left corner, snapped to whole pixels so sprites don't shimmer.</summary>
        public Vector2 TopLeft => new Vector2(
            MathF.Round(this.Position.X - (this.ViewSize.X / 2f)),
            MathF.Round(this.Position.Y - (this.ViewSize.Y / 2f)));

        public Matrix Transform => Matrix.CreateTranslation(-this.TopLeft.X, -this.TopLeft.Y, 0f);

        public Camera(Point viewSize, Rectangle worldBounds)
        {
            this.ViewSize = viewSize;
            this.WorldBounds = worldBounds;
            this.Position = new Vector2(worldBounds.Center.X, worldBounds.Center.Y);
        }

        /// <summary>Jump straight to a point (use when starting or restarting).</summary>
        public void SnapTo(Vector2 target)
        {
            this.Position = this.Clamp(target);
        }

        public void Follow(Vector2 target, float dt)
        {
            float t = 1f - (float)Math.Exp(-FollowSpeed * dt);      // frame-rate independent easing
            this.Position = this.Clamp(Vector2.Lerp(this.Position, target, t));
        }

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            return (screen + this.TopLeft);
        }

        public Vector2 WorldToScreen(Vector2 world)
        {
            return (world - this.TopLeft);
        }

        private Vector2 Clamp(Vector2 p)
        {
            float halfW = (this.ViewSize.X / 2f);
            float halfH = (this.ViewSize.Y / 2f);

            // if the map is smaller than the screen on an axis, just center on it
            float x = (this.WorldBounds.Width <= this.ViewSize.X)
                ? this.WorldBounds.Center.X
                : MathHelper.Clamp(p.X, this.WorldBounds.Left + halfW, this.WorldBounds.Right - halfW);

            float y = (this.WorldBounds.Height <= this.ViewSize.Y)
                ? this.WorldBounds.Center.Y
                : MathHelper.Clamp(p.Y, this.WorldBounds.Top + halfH, this.WorldBounds.Bottom - halfH);

            return new Vector2(x, y);
        }
    }
}