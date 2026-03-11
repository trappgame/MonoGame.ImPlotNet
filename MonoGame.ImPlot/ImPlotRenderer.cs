using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MonoGame.ImPlotNet
{
    /// <summary>
    /// MonoGame renderer backend for Dear ImGui (and ImPlot when used alongside it).
    ///
    /// ImPlot renders entirely through ImGui's draw lists, so no ImPlot-specific
    /// rendering code is needed here. Simply call ImPlot.CreateContext() after
    /// constructing this renderer, and ImPlot.DestroyContext() before Dispose().
    ///
    /// Inspired by MonoGame.ImGuiNet (https://github.com/tsMezotic/MonoGame.ImGuiNet)
    /// and the ImGui.NET XNA sample renderer.
    /// </summary>
    public class ImPlotRenderer : IDisposable
    {
        // ── GraphicsDevice ────────────────────────────────────────────────────
        private readonly GraphicsDevice _graphicsDevice;

        // ── Effect / rasterizer ───────────────────────────────────────────────
        private BasicEffect? _effect;
        private readonly RasterizerState _rasterizerState;

        // ── Vertex / index buffers ────────────────────────────────────────────
        private byte[] _vertexData = Array.Empty<byte>();
        private VertexBuffer? _vertexBuffer;
        private int _vertexBufferSize;

        private byte[] _indexData = Array.Empty<byte>();
        private IndexBuffer? _indexBuffer;
        private int _indexBufferSize;

        // ── Texture registry ──────────────────────────────────────────────────
        private readonly Dictionary<IntPtr, Texture2D> _loadedTextures = new();
        private int _textureId;
        private IntPtr? _fontTextureId;

        // ── Input state ───────────────────────────────────────────────────────
        private int _scrollWheelValue;
        private int _horizontalScrollWheelValue;
        private const float WheelDelta = 120f;
        private readonly Keys[] _allKeys = Enum.GetValues<Keys>();

        // ─────────────────────────────────────────────────────────────────────
        // Construction / initialisation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the renderer and sets up an ImGui context.
        /// Call <see cref="Initialize"/> once the GraphicsDevice is ready,
        /// and optionally call ImPlot.CreateContext() after this constructor.
        /// </summary>
        /// <param name="graphicsDevice">MonoGame GraphicsDevice.</param>
        /// <param name="window">GameWindow used for text-input events.</param>
        public ImPlotRenderer(GraphicsDevice graphicsDevice, GameWindow window)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            _rasterizerState = new RasterizerState
            {
                CullMode             = CullMode.None,
                DepthBias            = 0,
                FillMode             = FillMode.Solid,
                MultiSampleAntiAlias = false,
                ScissorTestEnable    = true,
                SlopeScaleDepthBias  = 0,
            };

            SetupInput(window);
        }

        /// <summary>
        /// Convenience constructor accepting a <see cref="Game"/> instance.
        /// </summary>
        public ImPlotRenderer(Game game)
            : this(game?.GraphicsDevice!, game?.Window!) { }

        /// <summary>
        /// Builds the font atlas texture. Call once after construction
        /// (and after loading any custom fonts into ImGui.GetIO().Fonts).
        /// </summary>
        public virtual void Initialize()
        {
            RebuildFontAtlas();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the ImGui font atlas and uploads it to the GPU.
        /// Call this after adding or changing fonts.
        /// </summary>
        public virtual unsafe void RebuildFontAtlas()
        {
            var io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);

            var pixels = new byte[width * height * bytesPerPixel];
            Marshal.Copy(new IntPtr(pixelData), pixels, 0, pixels.Length);

            var tex2d = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
            tex2d.SetData(pixels);

            if (_fontTextureId.HasValue)
                UnbindTexture(_fontTextureId.Value);

            _fontTextureId = BindTexture(tex2d);
            io.Fonts.SetTexID(_fontTextureId.Value);
            io.Fonts.ClearTexData();
        }

        /// <summary>
        /// Registers a MonoGame texture with ImGui and returns a handle
        /// suitable for <c>ImGui.Image()</c> calls.
        /// </summary>
        public virtual IntPtr BindTexture(Texture2D texture)
        {
            var id = new IntPtr(_textureId++);
            _loadedTextures.Add(id, texture);
            return id;
        }

        /// <summary>Removes a previously bound texture.</summary>
        public virtual void UnbindTexture(IntPtr textureId)
        {
            _loadedTextures.Remove(textureId);
        }

        /// <summary>
        /// Call at the start of your Update/Draw method.
        /// Updates ImGui IO state and calls <c>ImGui.NewFrame()</c>.
        /// </summary>
        public virtual void BeforeLayout(GameTime gameTime)
        {
            ImGui.GetIO().DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateInput();
            ImGui.NewFrame();
        }

        /// <summary>
        /// Call at the end of your Draw method (after all ImGui/ImPlot calls).
        /// Finalises the ImGui frame and renders it to the MonoGame back-buffer.
        /// </summary>
        public virtual void AfterLayout()
        {
            ImGui.Render();
            unsafe { RenderDrawData(ImGui.GetDrawData()); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────────────────────────────

        private void SetupInput(GameWindow window)
        {
            var io = ImGui.GetIO();
            window.TextInput += (_, a) =>
            {
                if (a.Character == '\t') return;
                io.AddInputCharacter(a.Character);
            };
        }

        private void UpdateInput()
        {
            var io       = ImGui.GetIO();
            var mouse    = Mouse.GetState();
            var keyboard = Keyboard.GetState();

            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, mouse.LeftButton   == ButtonState.Pressed);
            io.AddMouseButtonEvent(1, mouse.RightButton  == ButtonState.Pressed);
            io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(3, mouse.XButton1     == ButtonState.Pressed);
            io.AddMouseButtonEvent(4, mouse.XButton2     == ButtonState.Pressed);

            io.AddMouseWheelEvent(
                (mouse.HorizontalScrollWheelValue - _horizontalScrollWheelValue) / WheelDelta,
                (mouse.ScrollWheelValue           - _scrollWheelValue)           / WheelDelta);

            _scrollWheelValue           = mouse.ScrollWheelValue;
            _horizontalScrollWheelValue = mouse.HorizontalScrollWheelValue;

            foreach (var key in _allKeys)
            {
                if (TryMapKeys(key, out var imguiKey))
                    io.AddKeyEvent(imguiKey, keyboard.IsKeyDown(key));
            }

            io.DisplaySize = new System.Numerics.Vector2(
                _graphicsDevice.PresentationParameters.BackBufferWidth,
                _graphicsDevice.PresentationParameters.BackBufferHeight);
            io.DisplayFramebufferScale = System.Numerics.Vector2.One;
        }

        private static bool TryMapKeys(Keys key, out ImGuiKey imguiKey)
        {
            imguiKey = key switch
            {
                Keys.Back          => ImGuiKey.Backspace,
                Keys.Tab           => ImGuiKey.Tab,
                Keys.Enter         => ImGuiKey.Enter,
                Keys.CapsLock      => ImGuiKey.CapsLock,
                Keys.Escape        => ImGuiKey.Escape,
                Keys.Space         => ImGuiKey.Space,
                Keys.PageUp        => ImGuiKey.PageUp,
                Keys.PageDown      => ImGuiKey.PageDown,
                Keys.End           => ImGuiKey.End,
                Keys.Home          => ImGuiKey.Home,
                Keys.Left          => ImGuiKey.LeftArrow,
                Keys.Right         => ImGuiKey.RightArrow,
                Keys.Up            => ImGuiKey.UpArrow,
                Keys.Down          => ImGuiKey.DownArrow,
                Keys.PrintScreen   => ImGuiKey.PrintScreen,
                Keys.Insert        => ImGuiKey.Insert,
                Keys.Delete        => ImGuiKey.Delete,
                >= Keys.D0 and <= Keys.D9
                    => ImGuiKey._0 + (key - Keys.D0),
                >= Keys.A and <= Keys.Z
                    => ImGuiKey.A  + (key - Keys.A),
                >= Keys.NumPad0 and <= Keys.NumPad9
                    => ImGuiKey.Keypad0 + (key - Keys.NumPad0),
                Keys.Multiply      => ImGuiKey.KeypadMultiply,
                Keys.Add           => ImGuiKey.KeypadAdd,
                Keys.Subtract      => ImGuiKey.KeypadSubtract,
                Keys.Decimal       => ImGuiKey.KeypadDecimal,
                Keys.Divide        => ImGuiKey.KeypadDivide,
                >= Keys.F1 and <= Keys.F24
                    => ImGuiKey.F1 + (key - Keys.F1),
                Keys.NumLock       => ImGuiKey.NumLock,
                Keys.Scroll        => ImGuiKey.ScrollLock,
                Keys.LeftShift     => ImGuiKey.LeftShift,
                Keys.RightShift    => ImGuiKey.RightShift,
                Keys.LeftControl   => ImGuiKey.LeftCtrl,
                Keys.RightControl  => ImGuiKey.RightCtrl,
                Keys.LeftAlt       => ImGuiKey.LeftAlt,
                Keys.RightAlt      => ImGuiKey.RightAlt,
                Keys.LeftWindows   => ImGuiKey.LeftSuper,
                Keys.RightWindows  => ImGuiKey.RightSuper,
                Keys.OemSemicolon  => ImGuiKey.Semicolon,
                Keys.OemPlus       => ImGuiKey.Equal,
                Keys.OemComma      => ImGuiKey.Comma,
                Keys.OemMinus      => ImGuiKey.Minus,
                Keys.OemPeriod     => ImGuiKey.Period,
                Keys.OemQuestion   => ImGuiKey.Slash,
                Keys.OemTilde      => ImGuiKey.GraveAccent,
                Keys.OemOpenBrackets  => ImGuiKey.LeftBracket,
                Keys.OemCloseBrackets => ImGuiKey.RightBracket,
                Keys.OemPipe       => ImGuiKey.Backslash,
                Keys.OemQuotes     => ImGuiKey.Apostrophe,
                Keys.BrowserBack   => ImGuiKey.AppBack,
                Keys.BrowserForward => ImGuiKey.AppForward,
                _                  => ImGuiKey.None,
            };

            return imguiKey != ImGuiKey.None;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Rendering
        // ─────────────────────────────────────────────────────────────────────

        private Effect UpdateEffect(Texture2D texture)
        {
            _effect ??= new BasicEffect(_graphicsDevice);

            var io = ImGui.GetIO();
            _effect.World             = Matrix.Identity;
            _effect.View              = Matrix.Identity;
            _effect.Projection        = Matrix.CreateOrthographicOffCenter(
                0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
            _effect.TextureEnabled    = true;
            _effect.Texture           = texture;
            _effect.VertexColorEnabled = true;

            return _effect;
        }

        private unsafe void RenderDrawData(ImDrawDataPtr drawData)
        {
            // Save graphics state
            var lastViewport     = _graphicsDevice.Viewport;
            var lastScissor      = _graphicsDevice.ScissorRectangle;
            var lastRasterizer   = _graphicsDevice.RasterizerState;
            var lastDepthStencil = _graphicsDevice.DepthStencilState;
            var lastBlendFactor  = _graphicsDevice.BlendFactor;
            var lastBlendState   = _graphicsDevice.BlendState;

            _graphicsDevice.BlendFactor      = Color.White;
            _graphicsDevice.BlendState       = BlendState.NonPremultiplied;
            _graphicsDevice.RasterizerState  = _rasterizerState;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            _graphicsDevice.Viewport = new Viewport(
                0, 0,
                _graphicsDevice.PresentationParameters.BackBufferWidth,
                _graphicsDevice.PresentationParameters.BackBufferHeight);

            UpdateBuffers(drawData);
            RenderCommandLists(drawData);

            // Restore graphics state
            _graphicsDevice.Viewport          = lastViewport;
            _graphicsDevice.ScissorRectangle  = lastScissor;
            _graphicsDevice.RasterizerState   = lastRasterizer;
            _graphicsDevice.DepthStencilState = lastDepthStencil;
            _graphicsDevice.BlendState        = lastBlendState;
            _graphicsDevice.BlendFactor       = lastBlendFactor;
        }

        private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
        {
            if (drawData.TotalVtxCount == 0)
                return;

            // Grow vertex buffer if needed (1.5× growth factor)
            if (drawData.TotalVtxCount > _vertexBufferSize)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
                _vertexBuffer     = new VertexBuffer(
                    _graphicsDevice, DrawVertDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
                _vertexData = new byte[_vertexBufferSize * DrawVertDeclaration.Size];
            }

            // Grow index buffer if needed
            if (drawData.TotalIdxCount > _indexBufferSize)
            {
                _indexBuffer?.Dispose();
                _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
                _indexBuffer     = new IndexBuffer(
                    _graphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
                _indexData = new byte[_indexBufferSize * sizeof(ushort)];
            }

            // Copy ImGui draw data into managed byte arrays
            int vtxOffset = 0;
            int idxOffset = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];

                fixed (void* vtxDst = &_vertexData[vtxOffset * DrawVertDeclaration.Size])
                fixed (void* idxDst = &_indexData[idxOffset * sizeof(ushort)])
                {
                    Buffer.MemoryCopy(
                        (void*)cmdList.VtxBuffer.Data, vtxDst,
                        _vertexData.Length, cmdList.VtxBuffer.Size * DrawVertDeclaration.Size);
                    Buffer.MemoryCopy(
                        (void*)cmdList.IdxBuffer.Data, idxDst,
                        _indexData.Length, cmdList.IdxBuffer.Size * sizeof(ushort));
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }

            _vertexBuffer!.SetData(_vertexData, 0, drawData.TotalVtxCount * DrawVertDeclaration.Size);
            _indexBuffer!.SetData(_indexData,   0, drawData.TotalIdxCount * sizeof(ushort));
        }

        private void RenderCommandLists(ImDrawDataPtr drawData)
        {
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            int vtxOffset = 0;
            int idxOffset = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];

                for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
                {
                    var cmd = cmdList.CmdBuffer[cmdi];

                    if (cmd.ElemCount == 0)
                        continue;

                    if (!_loadedTextures.TryGetValue(cmd.TextureId, out var texture))
                        throw new InvalidOperationException(
                            $"ImPlotRenderer: texture with id '{cmd.TextureId}' is not registered. " +
                            "Use BindTexture() to register MonoGame textures before passing them to ImGui/ImPlot.");

                    _graphicsDevice.ScissorRectangle = new Rectangle(
                        (int)cmd.ClipRect.X,
                        (int)cmd.ClipRect.Y,
                        (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                        (int)(cmd.ClipRect.W - cmd.ClipRect.Y));

                    var effect = UpdateEffect(texture);

                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

#pragma warning disable CS0618
                        _graphicsDevice.DrawIndexedPrimitives(
                            primitiveType: PrimitiveType.TriangleList,
                            baseVertex:    (int)cmd.VtxOffset + vtxOffset,
                            minVertexIndex: 0,
                            numVertices:   cmdList.VtxBuffer.Size,
                            startIndex:    (int)cmd.IdxOffset + idxOffset,
                            primitiveCount: (int)cmd.ElemCount / 3);
#pragma warning restore CS0618
                    }
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // IDisposable
        // ─────────────────────────────────────────────────────────────────────

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_fontTextureId.HasValue)
            {
                UnbindTexture(_fontTextureId.Value);
                _fontTextureId = null;
            }

            _effect?.Dispose();
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _rasterizerState.Dispose();
        }
    }
}
