using System;

using GameProgrammingI_FinalProject.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameProgrammingI_FinalProject
{
    /// <summary>
    /// On-screen text and the player's health bar. Drawn above everything else.
    /// </summary>
    public class Hud : DrawableGameComponent
    {
        private readonly GameWorld world;

        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;

        public Hud(Game game, GameWorld world) : base(game)
        {
            this.world = world;
            this.DrawOrder = 90;
        }

        protected override void LoadContent()
        {
            this.spriteBatch = new SpriteBatch(this.GraphicsDevice);
            this.font = this.Game.Content.Load<SpriteFont>("Arial");
            this.pixel = Shapes.Pixel(this.GraphicsDevice);

            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = this.GraphicsDevice.Viewport;
            TimeSpan time = TimeSpan.FromSeconds(this.world.ElapsedTime);

            this.spriteBatch.Begin();

            // top-left: run stats
            string stats = string.Format("Kills: {0}    Zombies: {1}    Time: {2:00}:{3:00}",
                this.world.Kills, this.world.ZombieCount, (int)time.TotalMinutes, time.Seconds);
            this.spriteBatch.DrawString(this.font, stats, new Vector2(16f, 12f), Color.White);

            // bottom-left: player health
            float ratio = (this.world.Player.MaxHealth > 0f) ? (this.world.Player.Health / this.world.Player.MaxHealth) : 0f;
            Rectangle bar = new Rectangle(16, viewport.Height - 34, 240, 18);
            Shapes.DrawRect(this.spriteBatch, this.pixel, bar, Color.Black * 0.6f);
            Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), bar.Height), Color.Lerp(Color.Red, Color.LimeGreen, ratio));
            this.spriteBatch.DrawString(this.font, "HP", new Vector2(bar.Right + 8, bar.Y), Color.White);

            // controls hint
            this.spriteBatch.DrawString(this.font, "WASD: move   Mouse: aim   Hold LMB: shoot   Esc: quit   ~: debug",
                new Vector2(16f, viewport.Height - 56f), Color.White * 0.6f);

            if (this.world.IsGameOver)
            {
                this.DrawCentered("YOU DIED", viewport, viewport.Height / 2f - 30f, Color.Red);
                this.DrawCentered(string.Format("Zombies killed: {0}    Survived: {1:00}:{2:00}", this.world.Kills, (int)time.TotalMinutes, time.Seconds),
                    viewport, viewport.Height / 2f, Color.White);
                this.DrawCentered("Press R to restart", viewport, viewport.Height / 2f + 30f, Color.White);
            }

            this.spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawCentered(string text, Viewport viewport, float y, Color color)
        {
            Vector2 size = this.font.MeasureString(text);
            this.spriteBatch.DrawString(this.font, text, new Vector2((viewport.Width - size.X) / 2f, y), color);
        }
    }
}
