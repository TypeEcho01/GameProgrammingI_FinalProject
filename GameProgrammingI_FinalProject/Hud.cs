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

        private static readonly Color BuildingWaypointColor = Color.Orange;
        private static readonly Color CampWaypointColor = Color.Cyan;
        private const float WaypointMargin = 60f;       // how far in from the screen edge the arrows sit
        private const float PixelsPerMeter = 16f;       // only used for the distance readout

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

            this.spriteBatch.Begin();

            this.DrawWaypoints(gameTime, viewport);

            // top-left: run stats
            string line1 = string.Format
            (
                "Kills: {0}    Zombies: {1}    Buildings: {2}/{3}",
                this.world.Kills, this.world.ZombieCount, this.world.BuildingsSearched, this.world.BuildingCount
            );

            string line2 = string.Format
            (
                "Survivors:   Found: {0}/{1}   Following: {2}   Rescued: {3}   Lost: {4}",
                this.world.SurvivorsFound, this.world.TotalSurvivors, this.world.SurvivorsFollowing,
                this.world.SurvivorsRescued, this.world.SurvivorsDead
            );

            this.spriteBatch.DrawString(this.font, line1, new Vector2(16f, 12f), Color.White);
            this.spriteBatch.DrawString(this.font, line2, new Vector2(16f, 36f), Color.White);

            // evac countdown (top center)
            if (!this.world.IsGameOver)
            {
                TimeSpan left = TimeSpan.FromSeconds(Math.Ceiling(this.world.TimeLeft));

                Color timerColor = Color.White;
                if      (this.world.TimeLeft <= 30f) { timerColor = Color.Red; }
                else if (this.world.TimeLeft <= 60f) { timerColor = Color.Orange; }
                else if (this.world.TimeLeft <= 90f) { timerColor = Color.Yellow; }

                this.DrawCentered(string.Format("EVAC IN {0:00}:{1:00}", (int)left.TotalMinutes, left.Seconds), viewport, 12f, timerColor);

                if (this.world.HoldoutActive)
                {
                    string message = 
                    (
                        (this.world.Camp.Contains(this.world.Player.Location)) 
                        ? ("FINAL HOLDOUT: Survive!") 
                        : ("FINAL HOLDOUT: Get to the camp!")
                    );

                    this.DrawCentered(message, viewport, 36f, Color.Red);
                }
            }

            // bottom-left: player health
            float ratio = (this.world.Player.MaxHealth > 0f) ? (this.world.Player.Health / this.world.Player.MaxHealth) : 0f;
            Rectangle bar = new Rectangle(16, viewport.Height - 34, 240, 18);

            Shapes.DrawRect(this.spriteBatch, this.pixel, bar, Color.Black * 0.6f);

            Shapes.DrawRect
            (
                this.spriteBatch, this.pixel, 
                new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), bar.Height), 
                Color.Lerp(Color.Red, Color.LimeGreen, ratio)
            );
            
            this.spriteBatch.DrawString(this.font, "HP", new Vector2(bar.Right + 8, bar.Y), Color.White);

            // controls hint
            this.spriteBatch.DrawString
            (
                this.font, "WASD: move   Mouse: aim   Hold LMB: shoot   Esc: quit   ~: debug",
                new Vector2(16f, viewport.Height - 56f), (Color.White * 0.6f)
            );

            // door prompt and progress bar (bottom center)
            if (this.world.DoorTarget != null)
            {
                string prompt = "Hold E to open door";
                Vector2 promptSize = this.font.MeasureString(prompt);
                float promptY = viewport.Height - 110f;
                this.spriteBatch.DrawString(this.font, prompt, new Vector2((viewport.Width - promptSize.X) / 2f, promptY), Color.White);

                Rectangle searchBar = new Rectangle((viewport.Width / 2) - 100, ((int)(promptY) + 28), 200, 14);
                Shapes.DrawRect(this.spriteBatch, this.pixel, searchBar, Color.Black * 0.6f);
                Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(searchBar.X, searchBar.Y, (int)(searchBar.Width * this.world.SearchProgress), searchBar.Height), Color.Gold);
            }

            // "Survivor found!" / "Nothing here..."
            if (this.world.NoticeTimer > 0f)
            {
                this.DrawCentered(this.world.Notice, viewport, 60f, this.world.NoticeColor);
            }

            if (this.world.IsGameOver)
            {
                this.DrawResults(viewport);
            }

            this.spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawCentered(string text, Viewport viewport, float y, Color color)
        {
            Vector2 size = this.font.MeasureString(text);
            this.spriteBatch.DrawString(this.font, text, new Vector2(((viewport.Width - size.X) / 2f), y), color);
        }

        private void DrawWaypoints(GameTime gameTime, Viewport viewport)
        {
            if (this.world.IsGameOver)
            {
                return;
            }

            Building building = this.world.NearestUnsearchedBuilding();
            if (building != null)
            {
                this.DrawWaypoint(gameTime, viewport, building.Center, ((building.Bounds.Height / 2f) + 30f), "Building", BuildingWaypointColor);
            }

            if 
            (
                (this.world.SurvivorsFollowing > 0) 
                || ((this.world.HoldoutActive) && (!this.world.Camp.Contains(this.world.Player.Location)))
            )
            {
                this.DrawWaypoint(gameTime, viewport, this.world.Camp.Center, (Camp.Size / 2f) + 30f, "Camp", CampWaypointColor);
            }
        }

        private void DrawWaypoint(GameTime gameTime, Viewport viewport, Vector2 worldTarget, float markerHeight, string name, Color color)
        {
            Vector2 screenTarget = this.world.Camera.WorldToScreen(worldTarget);
            Vector2 center = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
            float meters = (Vector2.Distance(this.world.Player.Location, worldTarget) / PixelsPerMeter);
            string text = string.Format("{0} {1:0}m", name, meters);

            int margin = (int)WaypointMargin;
            Rectangle safe = new Rectangle(margin, margin, viewport.Width - (2 * margin), viewport.Height - (2 * margin));

            if (safe.Contains(screenTarget.ToPoint()))
            {
                // target is on screen: a bobbing arrow above it, pointing down
                float bob = ((float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4.0) * 4f);
                Vector2 marker = new Vector2(screenTarget.X, Math.Max((screenTarget.Y - markerHeight), 50f) + bob);

                this.DrawArrow(marker, MathHelper.PiOver2, color);
                this.DrawLabel(text, marker + new Vector2(0f, -30f), color);
                return;
            }

            // target is off screen: slide an arrow along the screen edge toward it
            Vector2 direction = (screenTarget - center);
            if (direction == Vector2.Zero)
            {
                return;
            }

            float scale = Math.Min
            (
                (center.X - WaypointMargin) / Math.Max(Math.Abs(direction.X), 0.001f),
                (center.Y - WaypointMargin) / Math.Max(Math.Abs(direction.Y), 0.001f)
            );

            Vector2 position = (center + (direction * scale));
            Vector2 unit = Vector2.Normalize(direction);

            this.DrawArrow(position, (float)Math.Atan2(direction.Y, direction.X), color);
            this.DrawLabel(text, position - (unit * 40f), color);
        }

        private void DrawArrow(Vector2 position, float angle, Color color)
        {
            float size = 16f;
            Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

            Vector2 tip = (position + (direction * size));
            Vector2 tail = (position - (direction * size));
            Vector2 barbA = (tip - (Vector2.Transform(direction, Matrix.CreateRotationZ(0.55f)) * size));
            Vector2 barbB = (tip - (Vector2.Transform(direction, Matrix.CreateRotationZ(-0.55f)) * size));

            // a dark outline first so the arrow reads against any background
            Shapes.DrawLine(this.spriteBatch, this.pixel, tail, tip,  Color.Black * 0.6f, 7f);
            Shapes.DrawLine(this.spriteBatch, this.pixel, tip, barbA, Color.Black * 0.6f, 7f);
            Shapes.DrawLine(this.spriteBatch, this.pixel, tip, barbB, Color.Black * 0.6f, 7f);

            Shapes.DrawLine(this.spriteBatch, this.pixel, tail, tip,  color, 4f);
            Shapes.DrawLine(this.spriteBatch, this.pixel, tip, barbA, color, 4f);
            Shapes.DrawLine(this.spriteBatch, this.pixel, tip, barbB, color, 4f);
        }

        private void DrawLabel(string text, Vector2 center, Color color)
        {
            Vector2 size = this.font.MeasureString(text);
            Vector2 position = (center - (size / 2f));

            this.spriteBatch.DrawString(this.font, text, position + new Vector2(1f, 1f), Color.Black);
            this.spriteBatch.DrawString(this.font, text, position, color);
        }

        private void DrawResults(Viewport viewport)
        {
            string title;
            Color color;
            switch (this.world.Result)
            {
                case MissionResult.FullSuccess: title = "MISSION COMPLETE"; color = Color.LimeGreen; break;
                case MissionResult.Success: title = "SUCCESS"; color = Color.GreenYellow; break;
                case MissionResult.Partial: title = "PARTIAL SUCCESS"; color = Color.Orange; break;
                default: title = "MISSION FAILED"; color = Color.Red; break;
            }

            string fate;
            switch (this.world.Fate)
            {
                case PlayerFate.Escaped: fate = "You escaped on the helicopter."; break;
                case PlayerFate.LeftBehind: fate = "You missed the helicopter and were left behind."; break;
                default: fate = "You died."; break;
            }

            TimeSpan time = TimeSpan.FromSeconds(this.world.ElapsedTime);

            Rectangle panel = new Rectangle((viewport.Width - 560) / 2, (viewport.Height - 320) / 2, 560, 320);
            Shapes.DrawRect(this.spriteBatch, this.pixel, panel, Color.Black * 0.8f);

            float y = (panel.Y + 24f);
            this.DrawCentered(title, viewport, y, color);
            this.DrawCentered(fate, viewport, (y + 40f), Color.White);

            this.DrawCentered(string.Format("Survivors rescued: {0} / {1}", this.world.SurvivorsRescued, this.world.TotalSurvivors), viewport, (y + 90f), Color.White);

            this.DrawCentered(string.Format("Survivors lost: {0}", this.world.SurvivorsDead), viewport, (y + 120f), Color.White);

            this.DrawCentered(string.Format("Zombies killed: {0}", this.world.Kills), viewport, (y + 150f), Color.White);

            this.DrawCentered(string.Format("Mission time: {0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds), viewport, (y + 180f), Color.White);

            this.DrawCentered("Press R to restart", viewport, (y + 250f), Color.Gold);
        }
    }
}
