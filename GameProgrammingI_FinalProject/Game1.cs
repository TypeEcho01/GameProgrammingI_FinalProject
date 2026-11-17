using GameProgrammingI_MonoGameLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameProgrammingI_FinalProject
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;

        // from GameProgrammingI_MonoGameLibrary
        private InputHandler _input;
        private GameConsole _console;

        private GameWorld _world;
        private Hud _hud;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // The library's GameConsole needs the InputHandler service, so the input goes in first.
            _input = new InputHandler(this);
            _input.UpdateOrder = -100;      // read input before anything uses it this frame
            Components.Add(_input);

            _console = new GameConsole(this);
            _console.DrawOrder = 100;       // on top of everything
            Components.Add(_console);

            _world = new GameWorld(this);
            Components.Add(_world);

            _hud = new Hud(this, _world);
            Components.Add(_hud);

            base.Initialize();

            _console.ToggleConsole();       // starts open; the ~ key opens it again for debug info
            _world.Reset();
        }

        protected override void Update(GameTime gameTime)
        {
            // updates every component: input, world, characters
            base.Update(gameTime);

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || _input.KeyboardState.IsKeyDown(Keys.Escape))
                Exit();

            if (_world.IsGameOver && _input.WasKeyPressed(Keys.R))
                _world.Reset();

            // debug info, visible with the ~ key
            _console.Log("Player HP", _world.Player.Health.ToString("0"));
            _console.Log("Facing (deg)", MathHelper.ToDegrees(_world.Player.FacingAngle).ToString("0"));
            _console.Log("Zombies", _world.ZombieCount.ToString());
            _console.Log("Characters", _world.Characters.Count.ToString());
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(34, 40, 34));

            // draws every component: world, zombies, player, hud, console
            base.Draw(gameTime);
        }
    }
}
