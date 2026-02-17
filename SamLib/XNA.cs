/*namespace SamLib.XNA
{
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using Microsoft.Xna.Framework.Input;
    using Microsoft.Xna.Framework.Content;
    using System.Dynamic;

    /// <summary>
    /// Camera 2D wrapper
    /// </summary>
    public class Camera2D
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; } = 1f;
        public float Rotation { get; set; } = 0f;

        public Matrix GetTransformationMatrix(GraphicsDevice device)
        {
            return Matrix.CreateTranslation(new Vector3(-Position.X, -Position.Y, 0)) *
                   Matrix.CreateRotationZ(Rotation) *
                   Matrix.CreateScale(new Vector3(Zoom, Zoom, 1)) *
                   Matrix.CreateTranslation(new Vector3(device.Viewport.Width * 0.5f, device.Viewport.Height * 0.5f, 0));
        }
    }

    Example usage:
    _spriteBatch.Begin(transformMatrix: _camera.GetTransformationMatrix(GraphicsDevice));

    /// <summary>
    /// Handles input
    /// </summary>
    public class InputHandler
    {
        private KeyboardState _currentKeys;
        private KeyboardState _previousKeys;
        private MouseState _currentMouse;
        private MouseState _previousMouse;

        public void Update()
        {
            _previousKeys = _currentKeys;
            _currentKeys = Keyboard.GetState();
            _previousMouse = _currentMouse;
            _currentMouse = Mouse.GetState();
        }

        public bool IsKeyPressed(Keys key) { return _currentKeys.IsKeyDown(key) && _previousKeys.IsKeyUp(key); }
        public bool IsKeyReleased(Keys key) { return _currentKeys.IsKeyUp(key) && _previousKeys.IsKeyDown(key); }
        public bool IsKeyDown(Keys key) { return _currentKeys.IsKeyDown(key); }
        public bool IsKeyUp(Keys key) { return _currentKeys.IsKeyUp(key); }
        public bool IsLeftMouseDown() => _currentMouse.LeftButton == ButtonState.Pressed;
        public bool IsLeftMousePressed() => _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        public bool IsLeftMouseReleased() => _currentMouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        public bool IsMiddleMouseDown() => _currentMouse.MiddleButton == ButtonState.Pressed;
        public bool IsMiddleMousePressed() => _currentMouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Released;
        public bool IsMiddleMouseReleased() => _currentMouse.MiddleButton == ButtonState.Released && _previousMouse.MiddleButton == ButtonState.Pressed;
        public bool IsRightMouseDown() => _currentMouse.RightButton == ButtonState.Pressed;
        public bool IsRightMousePressed() => _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        public bool IsRightMouseReleased() => _currentMouse.RightButton == ButtonState.Released && _previousMouse.RightButton == ButtonState.Pressed;
        public int GetScrollDelta() { return _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue; }
        /// <summary>
        /// Gets mouse position in world
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public Vector2 GetMouseWorldPosition(Camera2D camera, GraphicsDevice device)
        {
            // Undo matrix to get correct mouse window pos if a zoom or rotation is applied
            Matrix transform = camera.GetTransformationMatrix(device);
            Matrix inverseTransform = Matrix.Invert(transform);
            return Vector2.Transform(MouseWindowPosition, inverseTransform);
        }
        /// <summary>
        /// Mouse position relative to top-left of window
        /// </summary>
        public Vector2 MouseWindowPosition => new Vector2(_currentMouse.X, _currentMouse.Y);
        /// <summary>
        /// Pixel perfect mouse position
        /// </summary>
        public Point MouseWindowPoint => _currentMouse.Position; 
    }

    /// <summary>
    /// Scene
    /// </summary>
    public abstract class Scene
    {
        protected ContentManager? content;

        public virtual void Initialize() { }
        public abstract void LoadContent(ContentManager content);
        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch);
        public virtual void Unload()
        {
            content?.Unload(); 
            content = null;
        }
    }

    /// <summary>
    /// Scene manager
    /// </summary>
    public class SceneManager
    {
        public Scene? CurrentScene { get; private set; }

        public void LoadScene(Scene newScene, ContentManager content)
        {
            CurrentScene?.Unload();
            newScene.LoadContent(content);
            newScene.Initialize();
            CurrentScene = newScene;
        }

        public void Update(GameTime gameTime) => CurrentScene?.Update(gameTime);
        public void Draw(SpriteBatch spriteBatch) => CurrentScene?.Draw(spriteBatch);
    }

    // Entities
    public class Entity
    {
        public string Name { get; set; }
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale = Vector2.One;
        public bool IsActive = true;

        private List<Component> _components = new List<Component>();

        public T AddComponent<T>(T component) where T : Component
        {
            component.Parent = this;
            _components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            return _components.OfType<T>().FirstOrDefault();
        }

        public virtual void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            foreach (var component in _components) component.Update(gameTime);
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (!IsActive) return;
            foreach (var component in _components) component.Draw(spriteBatch);
        }
    }

    public abstract class Component
    {
        public Entity Parent; 
        public virtual void Initialize() { }
        public virtual void Update(GameTime gameTime) { }
        public virtual void Draw(SpriteBatch spriteBatch) { }
    }

    // Entity Components
    public class SpriteComponent : Component
    {
        public Texture2D Texture;
        public Color Tint = Color.White;
        public Vector2 Size; 

        public SpriteComponent(Texture2D texture = null) => Texture = texture;

        public override void Draw(SpriteBatch spriteBatch)
        {
            Texture2D tex = Texture ?? DebugUtils.GetPixel(spriteBatch.GraphicsDevice);

            Vector2 scale = Texture == null ? Size : Parent.Scale;

            spriteBatch.Draw(tex, Parent.Position, null, Tint,
                             Parent.Rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
    public struct AnimationClip
    {
        public int Row;
        public int FrameCount;
        public float Speed;
        public bool IsLooping;

        public AnimationClip(int row, int frameCount, float speed, bool isLooping = true)
        {
            Row = row;
            FrameCount = frameCount;
            Speed = speed;
            IsLooping = isLooping;
        }
    }

    public class AnimationComponent : Component
    {
        private Texture2D _sheet;
        private int _rows, _cols;
        private Dictionary<string, AnimationClip> _animations = new();

        private AnimationClip _currentClip;
        private int _currentFrame;
        private float _timer;

        public AnimationComponent(Texture2D sheet, int rows, int cols)
        {
            _sheet = sheet; _rows = rows; _cols = cols;
        }

        public void AddAnimation(string name, AnimationClip clip) => _animations[name] = clip;

        public void Play(string name, bool restartWhenAlreadyPlaying = false)
        {
            if (_animations.ContainsKey(name))
            {
                if (_currentClip.Equals(_animations[name]) && !restartWhenAlreadyPlaying) return;

                _currentClip = _animations[name];
                _currentFrame = 0;
                _timer = 0;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_currentClip.FrameCount == 0) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_timer >= _currentClip.Speed)
            {
                _timer = 0;
                _currentFrame++;

                if (_currentFrame >= _currentClip.FrameCount)
                {
                    _currentFrame = _currentClip.IsLooping ? 0 : _currentClip.FrameCount - 1;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            int frameWidth = _sheet.Width / _cols;
            int frameHeight = _sheet.Height / _rows;

            Rectangle sourceRect = new Rectangle(
                _currentFrame * frameWidth,
                _currentClip.Row * frameHeight,
                frameWidth,
                frameHeight
            );

            spriteBatch.Draw(_sheet, Parent.Position, sourceRect, Color.White,
                             Parent.Rotation, Vector2.Zero, Parent.Scale, SpriteEffects.None, 0f);
        }
    }

    // Debug
    public static class DebugUtils
    {
        private static Texture2D _pixel;

        public static Texture2D GetPixel(GraphicsDevice graphics)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(graphics, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }
            return _pixel;
        }
    }
}
*/