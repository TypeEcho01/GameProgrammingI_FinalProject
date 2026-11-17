using System;
using System.Collections.Generic;

using GameProgrammingI_FinalProject.Sprites;
using GameProgrammingI_FinalProject.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameProgrammingI_FinalProject
{
    /// <summary>
    /// The result of a hitscan trace.
    /// </summary>
    public struct RayHit
    {
        public Character Character;     // Character.Null if the ray hit nothing
        public float Distance;
        public Vector2 Point;           // where the ray ended (the hit point, or the end of its range)

        public bool DidHit => ((this.Character != null) && !this.Character.IsNull);
    }

    /// <summary>
    /// A short-lived line drawn when the gun fires.
    /// </summary>
    public class Tracer
    {
        public Vector2 Start;
        public Vector2 End;
        public float TimeLeft;
    }

    /// <summary>
    /// Owns every Character in the level and the rules that sit between them:
    /// spawning, collisions, the hitscan raycast, and win/lose bookkeeping.
    /// Characters are game components (like the rest of the library's sprites), so Add/Remove
    /// also register them with the Game so they update and draw themselves.
    /// </summary>
    public class GameWorld : DrawableGameComponent
    {
        // tuning
        private const int InitialZombies = 8;
        private const int MaxZombies = 40;
        private const float SpawnInterval = 2.0f;           // seconds between new zombies
        private const float MinSpawnDistance = 350f;        // never spawn right on top of the player
        private const float TracerLifetime = 0.07f;


        public readonly List<Character> Characters = new List<Character>();
        public readonly Random Random = new Random();

        public Character Player { get; private set; }

        public int Kills { get; private set; }
        public float ElapsedTime { get; private set; }

        public Rectangle Bounds => this.Game.GraphicsDevice.Viewport.Bounds;
        public bool IsGameOver => this.Player.IsDeadStatus;

        public int ZombieCount
        {
            get
            {
                int count = 0;
                foreach (Character character in this.Characters)
                {
                    if (character.IsZombieRole && character.IsNormalStatus)
                    {
                        count++;
                    }
                }
                return count;
            }
        }


        private readonly List<Tracer> tracers = new List<Tracer>();
        private float spawnTimer;

        private SpriteBatch spriteBatch;
        private Texture2D pixel;


        public GameWorld(Game game) : base(game)
        {
            this.Player = Character.Null;

            this.UpdateOrder = -50;     // after input, before the characters move
            this.DrawOrder = 0;         // under the characters
        }

        protected override void LoadContent()
        {
            this.spriteBatch = new SpriteBatch(this.GraphicsDevice);
            this.pixel = Shapes.Pixel(this.GraphicsDevice);

            base.LoadContent();
        }


        // setup
        /// <summary>
        /// Clears the level and starts a fresh run.
        /// </summary>
        public void Reset()
        {
            for (int i = this.Characters.Count - 1; i >= 0; i--)
            {
                this.Remove(this.Characters[i]);
            }
            this.tracers.Clear();

            this.Kills = 0;
            this.ElapsedTime = 0f;
            this.spawnTimer = SpawnInterval;

            Rectangle bounds = this.Bounds;
            this.Player = this.Add(Character.NewPlayer(this.Game, this, new Vector2(bounds.Center.X, bounds.Center.Y)));

            for (int i = 0; i < InitialZombies; i++)
            {
                this.SpawnZombie();
            }
        }

        public Character Add(Character character)
        {
            this.Characters.Add(character);
            this.Game.Components.Add(character);
            return character;
        }

        public void Remove(Character character)
        {
            this.Characters.Remove(character);
            this.Game.Components.Remove(character);
            character.Dispose();
        }

        private void SpawnZombie()
        {
            Rectangle bounds = this.Bounds;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2 location = new Vector2(
                    this.Random.Next(bounds.Left + 20, bounds.Right - 20),
                    this.Random.Next(bounds.Top + 20, bounds.Bottom - 20));

                if (Vector2.Distance(location, this.Player.Location) >= MinSpawnDistance)
                {
                    this.Add(Character.NewZombie(this.Game, this, location));
                    return;
                }
            }
        }


        // queries
        public Character FindNearestEnemy(Character from, float maxRange)
        {
            Character nearest = Character.Null;
            float nearestSquared = (maxRange * maxRange);

            foreach (Character other in this.Characters)
            {
                if (other == from || !other.IsNormalStatus || other.IsNoneTeam || other.Team == from.Team)
                {
                    continue;
                }

                float distanceSquared = Vector2.DistanceSquared(from.Location, other.Location);
                if (distanceSquared <= nearestSquared)
                {
                    nearest = other;
                    nearestSquared = distanceSquared;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Hitscan: finds the closest living enemy of the shooter along the ray.
        /// Characters are circles, so this is a ray-vs-circle test for each one.
        /// </summary>
        /// <param name="direction">must be a unit vector</param>
        public RayHit Raycast(Vector2 origin, Vector2 direction, float range, Character shooter)
        {
            RayHit best = new RayHit();
            best.Character = Character.Null;
            best.Distance = range;
            best.Point = origin + (direction * range);

            foreach (Character target in this.Characters)
            {
                if (target == shooter || !target.IsNormalStatus || target.IsNoneTeam || target.Team == shooter.Team)
                {
                    continue;
                }

                Vector2 toCenter = (target.Location - origin);
                float along = Vector2.Dot(toCenter, direction);                         // distance to the point closest to the circle's center
                float missSquared = toCenter.LengthSquared() - (along * along);         // how far the ray passes from the center, squared
                float radiusSquared = (target.Radius * target.Radius);

                if (missSquared > radiusSquared)
                {
                    continue;
                }

                float half = (float)Math.Sqrt(radiusSquared - missSquared);
                if ((along + half) < 0f)
                {
                    continue;       // circle is entirely behind the muzzle
                }

                float distance = Math.Max(0f, along - half);    // where the ray enters the circle (0 if we start inside it)
                if (distance < best.Distance)
                {
                    best.Character = target;
                    best.Distance = distance;
                    best.Point = origin + (direction * distance);
                }
            }

            return best;
        }

        public void AddTracer(Vector2 start, Vector2 end)
        {
            Tracer tracer = new Tracer();
            tracer.Start = start;
            tracer.End = end;
            tracer.TimeLeft = TracerLifetime;
            this.tracers.Add(tracer);
        }


        // game loop
        public override void Update(GameTime gameTime)
        {
            float dt = (float)(gameTime.ElapsedGameTime.TotalSeconds);

            for (int i = this.tracers.Count - 1; i >= 0; i--)
            {
                this.tracers[i].TimeLeft -= dt;
                if (this.tracers[i].TimeLeft <= 0f)
                {
                    this.tracers.RemoveAt(i);
                }
            }

            if (!this.IsGameOver)
            {
                this.ElapsedTime += dt;

                this.spawnTimer -= dt;
                if (this.spawnTimer <= 0f)
                {
                    this.spawnTimer = SpawnInterval;

                    if (this.ZombieCount < MaxZombies)
                    {
                        this.SpawnZombie();
                    }
                }
            }

            this.ResolveCollisions();
            this.RemoveDeadZombies();

            base.Update(gameTime);
        }

        /// <summary>
        /// Keeps bodies from overlapping. The player is never pushed; zombies get shoved out of
        /// the player and out of each other, so a horde spreads around you instead of stacking.
        /// </summary>
        private void ResolveCollisions()
        {
            for (int i = 0; i < this.Characters.Count; i++)
            {
                Character a = this.Characters[i];
                if (!a.IsNormalStatus)
                {
                    continue;
                }

                for (int j = i + 1; j < this.Characters.Count; j++)
                {
                    Character b = this.Characters[j];
                    if (!b.IsNormalStatus)
                    {
                        continue;
                    }

                    Vector2 delta = (b.Location - a.Location);
                    float distance = delta.Length();
                    float minDistance = (a.Radius + b.Radius);

                    if (distance >= minDistance)
                    {
                        continue;
                    }

                    Vector2 normal = (distance > 0.0001f) ? (delta / distance) : Vector2.UnitX;
                    float overlap = (minDistance - distance);

                    bool aMoves = !a.IsPlayerRole;
                    bool bMoves = !b.IsPlayerRole;

                    if (aMoves && bMoves)
                    {
                        a.Location -= (normal * (overlap / 2f));
                        b.Location += (normal * (overlap / 2f));
                    }
                    else if (aMoves)
                    {
                        a.Location -= (normal * overlap);
                    }
                    else if (bMoves)
                    {
                        b.Location += (normal * overlap);
                    }
                }
            }
        }

        private void RemoveDeadZombies()
        {
            for (int i = this.Characters.Count - 1; i >= 0; i--)
            {
                Character character = this.Characters[i];

                if (character.IsZombieRole && character.IsDeadStatus)
                {
                    this.Kills++;
                    this.Remove(character);
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            this.spriteBatch.Begin();

            // arena border
            Rectangle bounds = this.Bounds;
            Color border = new Color(80, 90, 80);
            int thickness = 4;
            Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness), border);
            Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness), border);
            Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height), border);
            Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height), border);

            // laser sight: shows where the gun is actually pointing right now (it lags the mouse because of TurnRate)
            if (this.Player.IsNormalStatus)
            {
                Vector2 muzzle = this.Player.Muzzle;
                RayHit aim = this.Raycast(muzzle, this.Player.FacingDirection, this.Player.Weapon.Range, this.Player);
                Shapes.DrawLine(this.spriteBatch, this.pixel, muzzle, aim.Point, Color.Red * 0.25f, 1f);
            }

            // shot tracers
            foreach (Tracer tracer in this.tracers)
            {
                Color color = Color.Yellow * (tracer.TimeLeft / TracerLifetime);
                Shapes.DrawLine(this.spriteBatch, this.pixel, tracer.Start, tracer.End, color, 3f);
            }

            this.spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
