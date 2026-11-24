using System;
using System.Collections.Generic;

using GameProgrammingI_FinalProject.Sprites;
using GameProgrammingI_FinalProject.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using GameProgrammingI_MonoGameLibrary.Util;
using Microsoft.Xna.Framework.Input;

namespace GameProgrammingI_FinalProject
{
    public enum MissionResult
    {
        InProgress,
        Failure,
        Partial,
        Success,
        FullSuccess
    }

    public enum PlayerFate
    {
        Escaped,
        LeftBehind,
        Died
    }

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
        private const int InitialZombies = 75;
        private const int MaxZombies = 120;
        private const float SpawnInterval = 1.25f;           // seconds between new zombies
        private const float MinSpawnDistance = 800f;        // farther than the screen is wide, so zombies spawn off-screen
        private const float TracerLifetime = 0.07f;

        public const int MapWidth = 4000;
        public const int MapHeight = 3000;

        private const float DoorReach = 50f;            // how close the player must be to a door to open it
        private const float SearchTime = 3f;            // seconds holding E to open a door

        private const float TimeLimit = (60f * 6);           // seconds until the evac leaves (6:00)
        private const float HoldoutTime = 60f;              // the final holdout starts when this many seconds are left
        private const int HoldoutZombies = 50;              // size of the first wave
        private const float HoldoutSpawnInterval = 0.8f;    // seconds between extra zombies during the holdout
        private const int HoldoutMaxZombies = 180;


        private struct OutcomeRule
        {
            public int Count;           // how many buildings are guaranteed to have this outcome
            public int MinGroup;        // group size is rolled between these (inclusive)
            public int MaxGroup;

            public OutcomeRule(int count, int minGroup, int maxGroup)
            {
                this.Count = count;
                this.MinGroup = minGroup;
                this.MaxGroup = maxGroup;
            }
        }

        // The number of buildings is the sum of the Count column.
        private static readonly Dictionary<SearchOutcome, OutcomeRule> OutcomeRules = new()
        {
            { SearchOutcome.Nothing,   new OutcomeRule(6, 0, 0) },
            { SearchOutcome.Survivors, new OutcomeRule(8, 1, 3) },
            { SearchOutcome.Zombies,   new OutcomeRule(6, 2, 5) },
        };


        public readonly List<Character> Characters = new List<Character>();
        public readonly Random Random = new Random();

        public Character Player { get; private set; }

        public int Kills { get; private set; }
        public float ElapsedTime { get; private set; }

        public Rectangle Bounds => new Rectangle(0, 0, MapWidth, MapHeight);
        public Camp Camp { get; } = new Camp(new Point(MapWidth / 2, MapHeight / 2));
        public Camera Camera { get; private set; }
        public bool IsGameOver => (this.Result != MissionResult.InProgress);

        public bool HoldoutActive => ((this.holdoutStarted) && (!this.IsGameOver));

        public readonly List<Building> Buildings = new List<Building>();

        public Building DoorTarget { get; private set; }    // closed door the player is close enough to open, or null
        public Color NoticeColor { get; private set; } = Color.White;
        public float SearchProgress { get; private set; }   // 0 to 1

        public int TotalSurvivors { get; private set; }
        public int SurvivorsFound { get; private set; }

        public string Notice { get; private set; } = "";
        public float NoticeTimer { get; private set; }

        public MissionResult Result { get; private set; } = MissionResult.InProgress;
        public PlayerFate Fate { get; private set; }
        public float TimeLeft => Math.Max(0f, this.evacAt - this.ElapsedTime);

        private IInputHandler input;

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

        public int BuildingCount => this.Buildings.Count;

        public int BuildingsSearched
        {
            get
            {
                int count = 0;
                foreach (Building building in this.Buildings)
                {
                    if (building.Searched)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int SurvivorsAlive
        {
            get
            {
                int count = 0;
                foreach (Character character in this.Characters)
                {
                    if (character.IsSurvivorRole && character.IsNormalStatus)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int SurvivorsDead
        {
            get
            {
                int count = 0;
                foreach (Character character in this.Characters)
                {
                    if (character.IsSurvivorRole && character.IsDeadStatus)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Living survivors that are not in the camp yet. Drives the camp waypoint.</summary>
        public int SurvivorsFollowing
        {
            get
            {
                int count = 0;
                foreach (Character character in this.Characters)
                {
                    if (character.IsSurvivorRole && character.IsNormalStatus && !this.Camp.Contains(character.Location))
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public int SurvivorsRescued
        {
            get
            {
                int count = 0;
                foreach (Character character in this.Characters)
                {
                    if (character.IsSurvivorRole && character.IsNormalStatus && character.IsRescued)
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

        private int survivorsDeadSeen;

        private int rescuedSeen;

        private float evacAt = TimeLimit;
        private bool evacCalledEarly;

        private bool holdoutStarted;


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

            Viewport viewport = this.GraphicsDevice.Viewport;
            this.Camera = new Camera(new Point(viewport.Width, viewport.Height), this.Bounds);

            this.input = this.Game.Services.GetService<IInputHandler>();

            base.LoadContent();
        }


        // setup
        /// <summary>
        /// Clears the level and starts a fresh run.
        /// </summary>
        public void Reset()
        {
            for (int i = (this.Characters.Count - 1); (i >= 0); i--)
            {
                this.Remove(this.Characters[i]);
            }
            this.tracers.Clear();

            this.SearchProgress = 0f;
            this.NoticeTimer = 0f;
            this.GenerateBuildings();

            this.SurvivorsFound = 0;
            this.survivorsDeadSeen = 0;
            this.rescuedSeen = 0;

            this.Result = MissionResult.InProgress;
            this.evacAt = TimeLimit;
            this.evacCalledEarly = false;
            this.holdoutStarted = false;

            this.Kills = 0;
            this.ElapsedTime = 0f;
            this.spawnTimer = SpawnInterval;

            this.Player = this.Add(Character.NewPlayer(this.Game, this, this.Camp.Center));
            this.Camera.SnapTo(this.Player.Location);

            this.SpawnSoldiers();

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
                Vector2 location = new Vector2
                (
                    x: this.Random.Next((bounds.Left + 20), (bounds.Right  - 20)),
                    y: this.Random.Next((bounds.Top  + 20), (bounds.Bottom - 20))
                );

                if 
                (
                    (Vector2.Distance(location, this.Player.Location) >= MinSpawnDistance )
                    && (!this.IsNearBuilding(location))
                    && (!this.IsInCamp(location))
                )
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

            foreach (Building building in this.Buildings)
            {
                foreach (Rectangle wall in building.Walls)
                {
                    if
                    (
                        (GameWorld.RayHitsRect(origin, direction, wall, out float wallDistance))
                        && (wallDistance < best.Distance)
                    )
                    {
                        best.Character = Character.Null;
                        best.Distance = wallDistance;
                        best.Point = origin + (direction * wallDistance);
                    }
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

            for (int i = (this.tracers.Count - 1); (i >= 0); i--)
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
                    if (this.holdoutStarted)
                    {
                        this.spawnTimer = HoldoutSpawnInterval;
                        if (this.ZombieCount < HoldoutMaxZombies)
                        {
                            this.SpawnHordeZombie();
                        }
                    }
                    else
                    {
                        this.spawnTimer = SpawnInterval;
                        if (this.ZombieCount < MaxZombies)
                        {
                            this.SpawnZombie();
                        }
                    }
                }

                this.UpdateDoors(dt);

                if (this.NoticeTimer > 0f)
                {
                    this.NoticeTimer -= dt;
                }
            }

            this.ResolveCollisions();
            this.RemoveDeadZombies();

            int survivorsDead = this.SurvivorsDead;
            if (survivorsDead > this.survivorsDeadSeen)
            {
                this.ShowNotice("A survivor was killed!", Color.OrangeRed);
            }
            this.survivorsDeadSeen = survivorsDead;

            int rescued = this.SurvivorsRescued;
            if (rescued > this.rescuedSeen)
            {
                this.ShowNotice("Survivor rescued!", Color.Cyan);
            }
            this.rescuedSeen = rescued;

            if (!this.IsGameOver)
            {
                this.UpdateMission();
            }

            Viewport viewport = this.GraphicsDevice.Viewport;
            this.Camera.ViewSize = new Point(viewport.Width, viewport.Height);

            if (!this.Player.IsNull)
            {
                this.Camera.Follow(this.Player.Location, dt);
            }

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

                    bool aMoves = ((!a.IsPlayerRole) && (!a.IsSoldierRole));
                    bool bMoves = ((!b.IsPlayerRole) && (!b.IsSoldierRole));

                    // player vs. soldier: the soldier holds their ground, so you're the one who gets shoved
                    if (!aMoves && !bMoves)
                    {
                        if (a.IsSoldierRole) { bMoves = true; }
                        else { aMoves = true; }
                    }

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
            this.spriteBatch.Begin(transformMatrix: this.Camera.Transform);

            // ground grid
            Color gridColor = (Color.White * 0.06f);

            for (int x = 0; x <= MapWidth; x += 100)
            {
                Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(x, 0, 1, MapHeight), gridColor);
            }

            for (int y = 0; y <= MapHeight; y += 100)
            {
                Shapes.DrawRect(this.spriteBatch, this.pixel, new Rectangle(0, y, MapWidth, 1), gridColor);
            }

            this.Camp.Draw(this.spriteBatch, this.pixel, Shapes.Circle(this.GraphicsDevice));

            // buildings: floor first, then walls
            foreach (Building building in this.Buildings)
            {
                Color floor = 
                (
                    (building.Searched) 
                    ? (new Color(38, 42, 48)) 
                    : (new Color(62, 68, 80))
                );

                Shapes.DrawRect(this.spriteBatch, this.pixel, building.Interior, floor);

                foreach (Rectangle wall in building.Walls)
                {
                    if (wall == building.DoorRect)
                    {
                        // closed door: boarded-up wood
                        Shapes.DrawRect(this.spriteBatch, this.pixel, wall, new Color(139, 90, 43));

                        Rectangle plank = (wall.Width > wall.Height)
                            ? new Rectangle(wall.X, wall.Center.Y - 1, wall.Width, 2)
                            : new Rectangle(wall.Center.X - 1, wall.Y, 2, wall.Height);
                        Shapes.DrawRect(this.spriteBatch, this.pixel, plank, new Color(95, 60, 28));
                    }
                    else
                    {
                        Shapes.DrawRect(this.spriteBatch, this.pixel, wall, new Color(150, 155, 165));
                    }
                }
            }

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

        // buildings
        private void GenerateBuildings()
        {
            this.Buildings.Clear();
            this.TotalSurvivors = 0;
            this.SurvivorsFound = 0;

            // 1. one plan (outcome + group size) per building, straight from the rules, then shuffled
            List<KeyValuePair<SearchOutcome, int>> plans = new List<KeyValuePair<SearchOutcome, int>>();
            foreach (KeyValuePair<SearchOutcome, OutcomeRule> rule in OutcomeRules)
            {
                for (int i = 0; i < rule.Value.Count; i++)
                {
                    int group = this.Random.Next(rule.Value.MinGroup, rule.Value.MaxGroup + 1);
                    plans.Add(new KeyValuePair<SearchOutcome, int>(rule.Key, group));
                }
            }

            for (int i = plans.Count - 1; i > 0; i--)
            {
                int j = this.Random.Next(i + 1);
                KeyValuePair<SearchOutcome, int> swap = plans[i];
                plans[i] = plans[j];
                plans[j] = swap;
            }

            // 2. place one building per plan

            // keep the camp in the middle clear
            Rectangle campZone = this.Camp.Zone;
            campZone.Inflate(150, 150);

            int planIndex = 0;
            int attempts = 0;
            while (planIndex < plans.Count && attempts < 1000)
            {
                attempts++;

                int w = this.Random.Next(220, 321);
                int h = this.Random.Next(180, 261);
                int x = this.Random.Next(100, MapWidth - 100 - w);
                int y = this.Random.Next(100, MapHeight - 100 - h);
                Rectangle area = new Rectangle(x, y, w, h);

                Rectangle padded = area;
                padded.Inflate(120, 120);       // leave a gap between buildings
                if (padded.Intersects(campZone))
                {
                    continue;
                }

                bool overlaps = false;
                foreach (Building other in this.Buildings)
                {
                    if (padded.Intersects(other.Bounds))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps)
                {
                    continue;
                }

                KeyValuePair<SearchOutcome, int> plan = plans[planIndex];
                this.Buildings.Add(new Building(area, (DoorSide)this.Random.Next(4), plan.Key, plan.Value));

                if (plan.Key == SearchOutcome.Survivors)
                {
                    this.TotalSurvivors += plan.Value;
                }

                planIndex++;
            }
        }

        private bool IsNearBuilding(Vector2 location)
        {
            Point point = location.ToPoint();

            foreach (Building building in this.Buildings)
            {
                Rectangle area = building.Bounds;
                area.Inflate(30, 30);
                if (area.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pushes a circular character out of any wall it overlaps. Call after moving it.
        /// </summary>
        public void PushOutOfWalls(Character character)
        {
            foreach (Building building in this.Buildings)
            {
                // cheap rejection: skip buildings the character isn't near
                Rectangle near = building.Bounds;
                near.Inflate((int)character.Radius + 2, (int)character.Radius + 2);
                if (!near.Contains(character.Location.ToPoint()))
                {
                    continue;
                }

                foreach (Rectangle wall in building.Walls)
                {
                    Vector2 location = character.Location;
                    Vector2 closest = new Vector2
                    (
                        x: MathHelper.Clamp(location.X, wall.Left, wall.Right),
                        y: MathHelper.Clamp(location.Y, wall.Top, wall.Bottom)
                    );

                    Vector2 delta = (location - closest);
                    float distanceSquared = delta.LengthSquared();

                    if (distanceSquared >= (character.Radius * character.Radius))
                    {
                        continue;
                    }

                    if (distanceSquared > 0.0001f)
                    {
                        float distance = (float)Math.Sqrt(distanceSquared);
                        character.Location = location + ((delta / distance) * (character.Radius - distance));
                    }
                    else
                    {
                        // center is inside the wall: leave through the nearest side
                        float left = location.X - wall.Left;
                        float right = wall.Right - location.X;
                        float top = location.Y - wall.Top;
                        float bottom = wall.Bottom - location.Y;
                        float nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));

                        if (nearest == left)       { character.Location = new Vector2(wall.Left - character.Radius, location.Y); }
                        else if (nearest == right) { character.Location = new Vector2(wall.Right + character.Radius, location.Y); }
                        else if (nearest == top)   { character.Location = new Vector2(location.X, wall.Top - character.Radius); }
                        else                       { character.Location = new Vector2(location.X, wall.Bottom + character.Radius); }
                    }
                }
            }
        }

        /// <summary>Ray vs. axis-aligned rectangle (the "slab" method). distance is how far along the ray it enters.</summary>
        private static bool RayHitsRect(Vector2 origin, Vector2 direction, Rectangle rect, out float distance)
        {
            float tMin = 0f;
            float tMax = float.MaxValue;
            distance = 0f;

            if (Math.Abs(direction.X) < 0.00001f)
            {
                if (origin.X < rect.Left || origin.X > rect.Right) { return false; }
            }
            else
            {
                float t1 = ((rect.Left - origin.X) / direction.X);
                float t2 = ((rect.Right - origin.X) / direction.X);
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }

            if (Math.Abs(direction.Y) < 0.00001f)
            {
                if (origin.Y < rect.Top || origin.Y > rect.Bottom) { return false; }
            }
            else
            {
                float t1 = ((rect.Top - origin.Y) / direction.Y);
                float t2 = ((rect.Bottom - origin.Y) / direction.Y);
                tMin = Math.Max(tMin, Math.Min(t1, t2));
                tMax = Math.Min(tMax, Math.Max(t1, t2));
            }

            if (tMin > tMax)
            {
                return false;
            }

            distance = tMin;
            return true;
        }

        // doors
        private void UpdateDoors(float dt)
        {
            Building nearest = null;
            float nearestDistance = DoorReach;

            if (this.Player.IsNormalStatus)
            {
                foreach (Building building in this.Buildings)
                {
                    if (building.DoorOpen)
                    {
                        continue;
                    }

                    float distance = DistanceToRect(this.Player.Location, building.DoorRect);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = building;
                    }
                }
            }

            if (nearest != this.DoorTarget)
            {
                this.SearchProgress = 0f;       // moved to a different door (or away): start over
            }
            this.DoorTarget = nearest;

            if ((nearest != null) && (this.input.KeyboardState.IsKeyDown(Keys.E)))
            {
                this.SearchProgress += (dt / SearchTime);

                if (this.SearchProgress >= 1f)
                {
                    nearest.OpenDoor();
                    this.FinishSearch(nearest);     // the door is open: reveal what's inside
                    this.DoorTarget = null;
                    this.SearchProgress = 0f;
                }
            }
            else
            {
                this.SearchProgress = 0f;
            }
        }

        private static float DistanceToRect(Vector2 point, Rectangle rect)
        {
            float dx = Math.Max(Math.Max(rect.Left - point.X, 0f), point.X - rect.Right);
            float dy = Math.Max(Math.Max(rect.Top - point.Y, 0f), point.Y - rect.Bottom);
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }


        // search results
        private void FinishSearch(Building building)
        {
            building.Searched = true;
            this.SearchProgress = 0f;

            switch (building.Outcome)
            {
                case SearchOutcome.Survivors:
                    for (int i = 0; i < building.GroupSize; i++)
                    {
                        this.Add(Character.NewSurvivor(this.Game, this, GameWorld.PointInside(building, i, building.GroupSize)));
                    }
                    this.SurvivorsFound += building.GroupSize;
                    this.ShowNotice((building.GroupSize == 1) ? "Survivor found!" : ("Found " + building.GroupSize + " survivors!"), Color.Gold);
                    break;

                case SearchOutcome.Zombies:
                    for (int i = 0; i < building.GroupSize; i++)
                    {
                        this.Add(Character.NewZombie(this.Game, this, GameWorld.PointInside(building, i, building.GroupSize)));
                    }
                    this.ShowNotice("Zombies!", Color.Red);
                    break;

                default:
                    this.ShowNotice("Nothing here...", Color.White);
                    break;
            }
        }

        /// <summary>Spreads a group out in a ring around the middle of the building.</summary>
        private static Vector2 PointInside(Building building, int index, int count)
        {
            if (count <= 1)
            {
                return building.Center;
            }

            float angle = (MathHelper.TwoPi * index / count);
            float radius = (Math.Min(building.Interior.Width, building.Interior.Height) * 0.25f);
            return building.Center + (new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius);
        }

        private void ShowNotice(string text, Color color)
        {
            this.Notice = text;
            this.NoticeColor = color;
            this.NoticeTimer = 2.5f;
        }

        private bool IsInCamp(Vector2 location)
        {
            Rectangle area = this.Camp.Zone;
            area.Inflate(100, 100);
            return area.Contains(location.ToPoint());
        }

        /// <summary>The closest building whose door hasn't been opened, or null when they're all done.</summary>
        public Building NearestUnsearchedBuilding()
        {
            Building nearest = null;
            float nearestSquared = float.MaxValue;

            foreach (Building building in this.Buildings)
            {
                if (building.Searched)
                {
                    continue;
                }

                float distanceSquared = Vector2.DistanceSquared(this.Player.Location, building.Center);
                if (distanceSquared < nearestSquared)
                {
                    nearestSquared = distanceSquared;
                    nearest = building;
                }
            }

            return nearest;
        }

        /// <summary>One soldier beside each gate of the camp, facing outward (a pinwheel, so every approach is covered).</summary>
        private void SpawnSoldiers()
        {
            Vector2 c = this.Camp.Center;

            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(-100f, -245f)), -MathHelper.PiOver2));     // top gate - left position
            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(+100f, -245f)), -MathHelper.PiOver2));     // top gate - right position

            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(+245f, -100f)), 0f));                       // right gate - top position
            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(+245f, +100f)), 0f));                       // right gate - bottom position

            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(-100f, +245f)), MathHelper.PiOver2));       // bottom gate - left position
            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(+100f, +245f)), MathHelper.PiOver2));       // bottom gate - right position

            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(-245f, -100f)), MathHelper.Pi));            // left gate - top position
            this.Add(Character.NewSoldier(this.Game, this, (c + new Vector2(-245f, +100f)), MathHelper.Pi));            // left gate - bottom position
        }

        // mission
        private void UpdateMission()
        {
            // dying ends the mission right away
            if (this.Player.IsDeadStatus)
            {
                this.EndMission(PlayerFate.Died);
                return;
            }

            // every survivor found, and everyone still alive is safe in the camp: skip ahead to the last minute
            if (!this.evacCalledEarly && this.TotalSurvivors > 0 && this.SurvivorsFound >= this.TotalSurvivors && this.SurvivorsFollowing == 0)
            {
                this.evacCalledEarly = true;

                if (this.TimeLeft > HoldoutTime)
                {
                    this.evacAt = (this.ElapsedTime + HoldoutTime);
                }
            }

            // the last minute: the horde comes
            if (!this.holdoutStarted && this.TimeLeft <= HoldoutTime)
            {
                this.StartHoldout();
            }

            // the helicopter leaves
            if (this.TimeLeft <= 0f)
            {
                this.EndMission(this.Camp.Contains(this.Player.Location) ? PlayerFate.Escaped : PlayerFate.LeftBehind);
            }
        }

        private void EndMission(PlayerFate fate)
        {
            this.Fate = fate;

            bool escaped = (fate == PlayerFate.Escaped);
            int rescued = this.SurvivorsRescued;
            int total = this.TotalSurvivors;

            if ((escaped) && (total > 0) && (rescued == total))
            {
                this.Result = MissionResult.FullSuccess;
            }
            else if ((escaped) && (rescued > 0) && ((rescued * 2) >= total))
            {
                this.Result = MissionResult.Success;
            }
            else if (rescued > 0)
            {
                this.Result = MissionResult.Partial;
            }
            else
            {
                this.Result = MissionResult.Failure;
            }
        }

        private void StartHoldout()
        {
            this.holdoutStarted = true;
            this.spawnTimer = HoldoutSpawnInterval;

            for (int i = 0; i < HoldoutZombies; i++)
            {
                this.SpawnHordeZombie();
            }

            this.ShowNotice
            (
                (
                    (this.evacCalledEarly) 
                    ? ("All survivors found! Hold out for the evac!") 
                    : ("FINAL HOLDOUT! Survive until the evac!")
                ), 

                Color.Red
            );
        }

        /// <summary>Spawns a zombie in a ring around the camp (off-screen), already marching on the player or the camp.</summary>
        private bool SpawnHordeZombie()
        {
            Rectangle inner = this.Bounds;
            inner.Inflate(-40, -40);

            for (int attempt = 0; attempt < 30; attempt++)
            {
                float angle = (float)(this.Random.NextDouble() * MathHelper.TwoPi);
                float distance = 1000f + (float)(this.Random.NextDouble() * 400f);
                Vector2 location = this.Camp.Center + (new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance);

                if (!inner.Contains(location.ToPoint()) || this.IsNearBuilding(location))
                {
                    continue;       // the ring runs off the map at the top and bottom: try another angle
                }

                float speed = 90f + (float)(this.Random.NextDouble() * 35f);      // varied, so they arrive in a stream, not a wall
                Character zombie = Character.NewZombie(this.Game, this, location, speed: speed);
                zombie.StartHunting(this.Random.Next(2) == 0);
                this.Add(zombie);
                return true;
            }

            return false;
        }
    }
}
