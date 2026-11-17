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
        Zombie
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

            // aim: the this.MouseState picks the angle we *want*, TurnRate limits how fast the gun gets there
            Vector2 toMouse = (new Vector2(this.MouseX, this.MouseY) - this.Location);
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
            if (this.IsDeadStatus)
            {
                return;
            }

            this.EnsureLoaded();

            Texture2D pixel = Shapes.Pixel(this.GraphicsDevice);

            // facing line: on the player it's the gun barrel, on a zombie it shows where it's heading
            float length = this.IsPlayerRole ? (this.Radius + 10f) : this.Radius;
            Vector2 tip = (this.Location + (this.FacingDirection * length));
            Shapes.DrawLine(sb, pixel, this.Location, tip, Color.Black * 0.6f, 3f);

            // health bar, only once hurt
            if (this.Health < this.MaxHealth)
            {
                int width = (int)(this.Radius * 2f) + 8;
                int x = (int)(this.X - (width / 2f));
                int y = (int)(this.Y - this.Radius - 10f);
                float ratio = (this.Health / this.MaxHealth);

                Shapes.DrawRect(sb, pixel, new Rectangle(x, y, width, 4), Color.Black * 0.7f);
                Shapes.DrawRect(sb, pixel, new Rectangle(x, y, (int)(width * ratio), 4), Color.Lerp(Color.Red, Color.LimeGreen, ratio));
            }
        }
    }
}
