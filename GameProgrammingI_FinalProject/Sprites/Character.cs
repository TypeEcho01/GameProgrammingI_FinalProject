#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GameProgrammingI_FinalProject.Util;
using GameProgrammingI_MonoGameLibrary.Sprite;
using GameProgrammingI_MonoGameLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameProgrammingI_FinalProject.Sprites
{
    public enum CharacterStatus
    {
        None, 
        Normal, 
        Dead
    }

    public enum Team
    {
        None, 
        Humans, 
        Zombies
    }

    public enum Role
    {
        None, 
        Player, 
        Zombie,
        Survivor,
        Soldier
    }

    /// <summary>
    /// Every character in the game (the player, zombies, later survivors) is a plain Character.
    /// What a character *is* comes from its Team and Role tags, and what it does each frame
    /// is picked from its Role. Use the static factory functions (NewPlayer, NewZombie) to make one.
    /// </summary>
    public class Character : DrawableSprite
    {
        // static
        public static readonly CharacterStatus NoneStatus = CharacterStatus.None;
        public static readonly CharacterStatus NormalStatus = CharacterStatus.Normal;
        public static readonly CharacterStatus DeadStatus = CharacterStatus.Dead;


        // declared after the statuses on purpose: static fields initialize in order.
        public static readonly Character Null = new Character(null, null, Team.None, Role.None, 0, 0f, 0f, 0f, Color.Transparent);


        // tuning
        private const float ContactReach = 4f;          // how close (past touching) a zombie must be to bite
        private const float WanderSpeedScale = 0.35f;   // idle zombies shamble at this fraction of full speed
        private const float HitFlashTime = 0.08f;

        private const float FollowStopDistance = 70f;       // survivors stop this far from the player
        private const float FollowResumeDistance = 100f;    // ...and only start walking again once you're this far away


        // tags
        public CharacterStatus Status { get; private set; }
        public Team Team { get; private set; }
        public Role Role { get; private set; }


        // on spawn
        public Character LastAttacker { get; private set; }
        public float FacingAngle { get; private set; }      // radians, 0 = right, positive = clockwise on screen


        // initalizers
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        
        public float TurnRate { get; private set; }         // radians per second

        public GameWorld World { get; private set; }
        public float Radius { get; private set; }

        public Weapon Weapon { get; private set; }          // only the player gets one for now
        public float ContactDamagePerSecond { get; private set; }
        public float SightRange { get; private set; }


        // accessors
        public float X => this.Location.X;
        public float Y => this.Location.Y;

        public float MouseX => this.input.MouseState.X;
        public float MouseY => this.input.MouseState.Y;

        public bool IsNull => (object.ReferenceEquals(this, Character.Null));

        public bool IsInitalized => (this.Status != NoneStatus);

        public bool IsNoneStatus => (this.Status == NoneStatus);
        public bool IsNormalStatus => (this.Status == NormalStatus);
        public bool IsDeadStatus => (this.Status == DeadStatus);

        public bool IsNoneTeam => (this.Team == Team.None);
        public bool IsHumanTeam => (this.Team == Team.Humans);
        public bool IsZombieTeam => (this.Team == Team.Zombies);

        public bool IsNoneRole => (this.Role == Role.None);
        public bool IsPlayerRole => (this.Role == Role.Player);
        public bool IsZombieRole => (this.Role == Role.Zombie);
        public bool IsSurvivorRole => (this.Role == Role.Survivor);
        public bool IsSoldierRole => (this.Role == Role.Soldier);

        public bool IsRescued { get; private set; }         // a survivor that has reached the camp

        public Vector2 FacingDirection => new Vector2((float)Math.Cos(this.FacingAngle), (float)Math.Sin(this.FacingAngle));

        /// <summary>Where shots leave the character: the edge of the body, along the facing direction.</summary>
        public Vector2 Muzzle => (this.Location + (this.FacingDirection * this.Radius));


        // private state
        private IInputHandler? input;
        private IInputHandler Input => (this.input ??= this.Game.Services.GetService<IInputHandler>());
        private Color baseColor;
        private float hitFlash;
        private float wanderAngle;
        private float wanderTimer;
        private bool isWalking;
        private Vector2 restPoint;
        private bool isHunting;
        private bool huntsPlayer;


        private Character(Game game, GameWorld world, Team team, Role role, short health, float turnRate, float radius, float speed, Color color) : base(game)
        {
            // tags
            this.Status = NoneStatus;
            this.Team = team;
            this.Role = role;
            

            // on spawn
            this.LastAttacker = Character.Null;
            this.FacingAngle = 0;


            // initalizers
            this.Health = this.MaxHealth = (float)(health);
            this.TurnRate = turnRate;

            this.World = world;
            this.Radius = radius;
            this.Speed = speed;
            this.baseColor = color;
        }


        // factories
        public static Character NewPlayer(Game game, GameWorld world, Vector2 location)
        {
            Character player = new Character
            (
                game, world, Team.Humans, Role.Player,
                health: 100,
                turnRate: MathHelper.ToRadians(420f),   // the gun swings at a fixed speed toward the mouse
                radius: 16f,
                speed: 220f,
                color: new Color(80, 160, 255)
            );

            player.Location = location;
            player.Weapon = new Weapon(damage: 25f, range: 2000f, fireInterval: 0.18f);
            player.DrawOrder = 20;

            player.Activate();
            return player;
        }

        // The optional parameters make other zombie types (big/slow, small/fast) a one-line addition later.
        public static Character NewZombie(Game game, GameWorld world, Vector2 location, short health = 50, float speed = 90f, float radius = 14f)
        {
            Character zombie = new Character
            (
                game, world, Team.Zombies, Role.Zombie,
                health: health,
                turnRate: MathHelper.ToRadians(200f),
                radius: radius,
                speed: speed,
                color: new Color(90, 170, 70)
            );

            zombie.Location = location;
            zombie.ContactDamagePerSecond = 20f;
            zombie.SightRange = 450f;
            zombie.FacingAngle = (float)((world.Random.NextDouble() * MathHelper.TwoPi) - MathHelper.Pi);
            zombie.DrawOrder = 10;

            zombie.Activate();
            return zombie;
        }

        public static Character NewSurvivor(Game game, GameWorld world, Vector2 location)
        {
            Character survivor = new Character
            (
                game, world, Team.Humans, Role.Survivor,
                health: 40,
                turnRate: MathHelper.ToRadians(360f),
                radius: 12f,
                speed: 150f,
                color: new Color(242, 177, 52)
            );

            survivor.Location = location;
            survivor.DrawOrder = 15;

            survivor.Activate();
            return survivor;
        }

        public static Character NewSoldier(Game game, GameWorld world, Vector2 location, float facingAngle = 0f)
        {
            Character soldier = new Character
            (
                game, world, Team.Humans, Role.Soldier,
                health: 150,
                turnRate: MathHelper.ToRadians(300f),
                radius: 16f,
                speed: 0f,                  // soldiers hold their position
                color: new Color(85, 107, 47)
            );

            soldier.Location = location;
            soldier.FacingAngle = facingAngle;
            soldier.SightRange = 500f;
            soldier.Weapon = new Weapon(damage: 20f, range: 600f, fireInterval: 0.5f);
            soldier.DrawOrder = 15;

            soldier.Activate();
            return soldier;
        }

        private void EnsureLoaded()
        {
            if (this.spriteBatch is null)
            {
                this.LoadContent();
            }
        }

        private void Activate()
        {
            this.Status = NormalStatus;
        }


        // component lifecycle
        public override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Creates the SpriteBatch, loads the SpriteMarker texture, resets Origin and DrawColor.
            base.LoadContent();

            // Placeholder art: one shared circle texture, scaled to this character's size.
            this.SpriteTexture = Shapes.Circle(this.GraphicsDevice);
            this.Origin = new Vector2((Shapes.CircleSize / 2f), (Shapes.CircleSize / 2f));     // Location is the center
            this.Scale = ((this.Radius * 2f) / Shapes.CircleSize);
            this.DrawColor = this.baseColor;
        }

        protected override void Dispose(bool disposing)
        {
            if ((disposing) && (this.spriteBatch is not null))
            {
                this.spriteBatch?.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void Update(GameTime gameTime)
        {
            this.EnsureLoaded();

            float dt = (float)(gameTime.ElapsedGameTime.TotalSeconds);

            if (this.IsNormalStatus)
            {
                // What a character does is decided by its Role, not by its class.
                switch (this.Role)
                {
                    case Role.Player:
                        this.UpdatePlayer(dt);
                        break;

                    case Role.Zombie:
                        this.UpdateZombie(dt);
                        break;

                    case Role.Survivor:
                        this.UpdateSurvivor(dt);
                        break;

                    case Role.Soldier:
                        this.UpdateSoldier(dt);
                        break;
                }
            }

            if (this.hitFlash > 0f)
            {
                this.hitFlash -= dt;
            }

            if (this.IsDeadStatus)
            {
                this.DrawColor = Color.DimGray;
            }
            else
            {
                this.DrawColor = ((this.hitFlash > 0f) ? (Color.White) : (this.baseColor));
            }

            base.Update(gameTime);
        }

        private bool IsKeyDown(Keys key)
        {
            return this.Input.KeyboardState.IsKeyDown(key);
        }

        enum MouseButtons
        {
            None, 
            LeftButton, 
            RightButton, 
            MiddleButton
        }

        private bool IsMouseButtonDown(MouseButtons button)
        {
            switch (button)
            {
                case (MouseButtons.LeftButton):
                {
                    return (this.Input.MouseState.LeftButton == ButtonState.Pressed);
                }
                case (MouseButtons.RightButton):
                {
                    return (this.Input.MouseState.RightButton == ButtonState.Pressed);
                }
                case (MouseButtons.MiddleButton):
                {
                    return (this.Input.MouseState.MiddleButton == ButtonState.Pressed);
                }
                default:
                {
                    return false;
                }
            }
        }

        // role behavior
        private void UpdatePlayer(float dt)
        {
            if (this.World.IsGameOver)
            {
                this.Direction = Vector2.Zero;      // the mission is over: freeze the player
                return;
            }

            // move: WASD / arrows, independent of where the gun points
            Vector2 move = Vector2.Zero;

            if ((this.IsKeyDown(Keys.W)) || (this.IsKeyDown(Keys.Up)))    { move.Y -= 1f; }
            if ((this.IsKeyDown(Keys.S)) || (this.IsKeyDown(Keys.Down)))  { move.Y += 1f; }
            if ((this.IsKeyDown(Keys.A)) || (this.IsKeyDown(Keys.Left)))  { move.X -= 1f; }
            if ((this.IsKeyDown(Keys.D)) || (this.IsKeyDown(Keys.Right))) { move.X += 1f; }

            if (move != Vector2.Zero)
            {
                move.Normalize();
            }

            this.Direction = move;
            this.Location += (move * this.Speed * dt);
            this.ClampToWorld();

            this.World.PushOutOfWalls(this);

            // aim: the this.MouseState picks the angle we *want*, TurnRate limits how fast the gun gets there
            Vector2 mouseWorld = this.World.Camera.ScreenToWorld(new Vector2(this.MouseX, this.MouseY));
            Vector2 toMouse = (mouseWorld - this.Location);

            if (toMouse.LengthSquared() > 4f)
            {
                this.TurnToward((float)Math.Atan2(toMouse.Y, toMouse.X), dt);
            }

            // shoot: hold the left this.MouseState button
            this.Weapon.Update(dt);
            if (this.IsMouseButtonDown(MouseButtons.LeftButton))
            {
                this.Weapon.TryFire(this);
            }
        }

        private void UpdateZombie(float dt)
        {
            if (this.World.IsGameOver)
            {
                this.Direction = Vector2.Zero;      // the mission is over: freeze the zombie
                return;
            }

            // Chase the closest enemy we can see, or whoever shot us.
            Character target = this.World.FindNearestEnemy(this, this.SightRange);
            if ((target.IsNull) && (!this.LastAttacker.IsNull) && (this.LastAttacker.IsNormalStatus))
            {
                target = this.LastAttacker;
            }

            float speed;
            if (!target.IsNull)
            {
                Vector2 toTarget = (target.Location - this.Location);
                this.TurnToward((float)Math.Atan2(toTarget.Y, toTarget.X), dt);

                bool touching = (toTarget.Length() <= (this.Radius + target.Radius + ContactReach));
                if (touching)
                {
                    this.DealDamage(target, this.ContactDamagePerSecond * dt);
                    speed = 0f;
                }
                else
                {
                    speed = this.Speed;
                }
            }
            else if (this.isHunting)
            {
                // nobody in sight yet: march on the player or the camp
                Vector2 goal = (this.huntsPlayer && this.World.Player.IsNormalStatus) ? this.World.Player.Location : this.World.Camp.Center;
                Vector2 toGoal = (goal - this.Location);

                this.TurnToward((float)Math.Atan2(toGoal.Y, toGoal.X), dt);
                speed = this.Speed;
            }
            else
            {
                // idle: shamble around in random directions
                this.wanderTimer -= dt;
                if (this.wanderTimer <= 0f)
                {
                    this.wanderAngle = (float)((this.World.Random.NextDouble() * MathHelper.TwoPi) - MathHelper.Pi);
                    this.wanderTimer = 1f + (float)(this.World.Random.NextDouble() * 2.0);
                }

                this.TurnToward(this.wanderAngle, dt);
                speed = this.Speed * WanderSpeedScale;
            }

            // zombies always walk the way they're facing
            this.Direction = this.FacingDirection;
            this.Location += (this.Direction * speed * dt);

            if (this.ClampToWorld())
            {
                this.wanderTimer = 0f;      // hit a wall, pick a new direction
            }

            this.World.PushOutOfWalls(this);
        }

        private void UpdateSurvivor(float dt)
        {
            if (this.World.IsGameOver)
            {
                this.Direction = Vector2.Zero;      // the mission is over: freeze the survivor
                return;
            }

            // reached the camp: stay there for good
            if ((!this.IsRescued) && (this.World.Camp.Contains(this.Location)))
            {
                this.IsRescued = true;
                this.restPoint = this.World.Camp.RandomRestPoint(this.World.Random);
            }

            if (this.IsRescued)
            {
                // walk to a spot inside the camp, then stand there
                Vector2 toRest = (this.restPoint - this.Location);
                float restDistance = toRest.Length();

                if (restDistance > 6f)
                {
                    this.TurnToward((float)Math.Atan2(toRest.Y, toRest.X), dt);
                    this.Direction = (toRest / restDistance);
                    this.Location += (this.Direction * this.Speed * 0.6f * dt);
                }
                return;
            }

            Character leader = this.World.Player;

            if (!leader.IsNormalStatus)
            {
                return;         // nobody left to follow
            }

            Vector2 toLeader = (leader.Location - this.Location);
            float distance = toLeader.Length();

            // two thresholds so they don't jitter at the edge of the follow distance
            if (distance > FollowResumeDistance)
            {
                this.isWalking = true;
            }
            else if (distance < FollowStopDistance)
            {
                this.isWalking = false;
            }

            if (distance > 0.001f)
            {
                this.TurnToward((float)Math.Atan2(toLeader.Y, toLeader.X), dt);
            }

            if (this.isWalking)
            {
                this.Direction = (toLeader / distance);
                this.Location += (this.Direction * this.Speed * dt);

                this.ClampToWorld();
                this.World.PushOutOfWalls(this);
            }
        }

        private void UpdateSoldier(float dt)
        {
            if (this.World.IsGameOver)
            {
                this.Direction = Vector2.Zero;      // the mission is over: freeze the soldier
                return;
            }

            this.Weapon.Update(dt);

            Character target = this.World.FindNearestEnemy(this, this.SightRange);
            if (target.IsNull)
            {
                return;         // nothing to shoot: stand guard
            }

            Vector2 toTarget = (target.Location - this.Location);
            float targetAngle = (float)Math.Atan2(toTarget.Y, toTarget.X);
            this.TurnToward(targetAngle, dt);

            // only fire once the gun is lined up, and only if the shot isn't blocked by a wall
            float aimError = Math.Abs(MathHelper.WrapAngle(targetAngle - this.FacingAngle));
            if (aimError < 0.05f)
            {
                RayHit hit = this.World.Raycast(this.Muzzle, this.FacingDirection, this.Weapon.Range, this);
                if (hit.DidHit)
                {
                    this.Weapon.TryFire(this);
                }
            }
        }


        // movement helpers
        private void TurnToward(float targetAngle, float dt)
        {
            float difference = MathHelper.WrapAngle(targetAngle - this.FacingAngle);
            float step = (this.TurnRate * dt);

            if (Math.Abs(difference) <= step)
            {
                this.FacingAngle = targetAngle;
            }
            else
            {
                this.FacingAngle = MathHelper.WrapAngle(this.FacingAngle + (Math.Sign(difference) * step));
            }
        }

        /// <returns>true if the character had to be moved back inside the world</returns>
        private bool ClampToWorld()
        {
            Rectangle bounds = this.World.Bounds;
            Vector2 clamped = new Vector2
            (
                x: MathHelper.Clamp(this.X, bounds.Left + this.Radius, bounds.Right - this.Radius),
                y: MathHelper.Clamp(this.Y, bounds.Top + this.Radius, bounds.Bottom - this.Radius)
            );

            bool changed = (clamped != this.Location);
            this.Location = clamped;

            return changed;
        }


        // health
        public float TakeDamage(float damage, Character? attacker = null)
        {
            if (!this.IsNormalStatus)
            {
                return 0f;
            }

            this.LastAttacker = ((attacker is not null) ? (attacker) : (Character.Null));

            this.hitFlash = HitFlashTime;

            float newHealth = (this.Health - damage);

            if (newHealth <= 0)
            {
                float damageDealt = (damage + newHealth);

                this.Die();

                return damageDealt;
            }

            this.Health = newHealth;
            return damage;
        }

        public float DealDamage(Character other, float damage)
        {
            if (other.IsNull)
            {
                return 0f;
            }

            return other.TakeDamage(damage, this);
        }

        public void Die()
        {
            this.Health = 0;

            this.Status = CharacterStatus.Dead;
        }


        // drawing
        public override void Draw(SpriteBatch sb)
        {
            // the circle body (and the sprite markers, if ShowMarkers is on)
            base.Draw(sb);

            if (this.IsDeadStatus)
            {
                return;
            }

            this.DrawRoleDetails(sb);

            // health bar, only once hurt
            if (this.Health < this.MaxHealth)
            {
                Texture2D pixel = Shapes.Pixel(this.GraphicsDevice);

                int width = (int)(this.Radius * 2f) + 8;
                int x = (int)(this.X - (width / 2f));
                int y = (int)(this.Y - this.Radius - 10f);
                float ratio = (this.Health / this.MaxHealth);

                Shapes.DrawRect(sb, pixel, new Rectangle(x, y, width, 4), Color.Black * 0.7f);
                Shapes.DrawRect(sb, pixel, new Rectangle(x, y, (int)(width * ratio), 4), Color.Lerp(Color.Red, Color.LimeGreen, ratio));
            }
        }

        /// <summary>Draws the extra circles/lines that make each Role look different.</summary>
        private void DrawRoleDetails(SpriteBatch sb)
        {
            Texture2D pixel = Shapes.Pixel(this.GraphicsDevice);
            Vector2 facing = this.FacingDirection;

            switch (this.Role)
            {
                case Role.Player:
                    Shapes.DrawLine(sb, pixel, this.Location, this.Location + (facing * (this.Radius + 10f)), Color.Black * 0.7f, 3f);
                    this.DrawDisc(sb, this.Location, this.Radius * 1.0f, new Color(200, 225, 255));
                    break;

                case Role.Soldier:
                    Shapes.DrawLine(sb, pixel, this.Location, this.Location + (facing * (this.Radius + 10f)), Color.Black * 0.7f, 3f);
                    this.DrawDisc(sb, this.Location, this.Radius * 1.2f, new Color(50, 65, 30));
                    break;

                case Role.Survivor:
                    this.DrawDisc
                    (
                        sb, this.Location, (this.Radius * 0.9f), 
                        ((this.IsRescued) ? (new Color(170, 235, 245)) : (new Color(255, 236, 190)))
                    );
                    break;

                case Role.Zombie:
                    this.DrawDisc(sb, this.Location, this.Radius * 0.8f, new Color(50, 110, 45));

                    // two arms reaching out in front
                    float armDistance = (this.Radius * 0.85f);
                    float armSize = (this.Radius * 0.45f);
                    foreach (float offset in new float[] { -0.6f, 0.6f })
                    {
                        float angle = (this.FacingAngle + offset);
                        Vector2 hand = this.Location + (new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * armDistance);
                        this.DrawDisc(sb, hand, armSize, new Color(120, 190, 95));
                    }
                    break;
            }
        }

        private void DrawDisc(SpriteBatch sb, Vector2 center, float diameter, Color color)
        {
            Texture2D disc = Shapes.Circle(this.GraphicsDevice);
            Vector2 origin = new Vector2(Shapes.CircleSize / 2f, Shapes.CircleSize / 2f);

            sb.Draw(disc, center, null, color, 0f, origin, diameter / Shapes.CircleSize, SpriteEffects.None, 0f);
        }

        public override void Draw(GameTime gameTime)
        {
            // Same as DrawableSprite.Draw but through the camera. Don't call base.Draw:
            // it would draw the character a second time, without the camera.

            this.spriteBatch.Begin(transformMatrix: this.World.Camera.Transform);

            this.Draw(this.spriteBatch);

            this.spriteBatch.End();
        }

        /// <summary>Makes a zombie march across the map even with nobody in sight. Used for the final holdout.</summary>
        public void StartHunting(bool targetPlayer)
        {
            this.isHunting = true;
            this.huntsPlayer = targetPlayer;
        }
    }
}
