using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MonoGame.ImPlotNet
{
    /// <summary>
    /// MonoGame renderer backend for Dear ImGui (Hexa.NET.ImGui) and ImPlot (Hexa.NET.ImPlot).
    ///
    /// ImPlot renders entirely through ImGui's draw lists, so no ImPlot-specific
    /// rendering code is needed here. After constructing this renderer:
    ///   1. Call Initialize() to signal that the backend is ready.
    ///   2. Call ImPlot.CreateContext().
    ///   3. Call ImPlot.SetImGuiContext(renderer.ImGuiContext).
    ///   4. On shutdown: ImPlot.DestroyContext() then renderer.Dispose().
    ///
    /// Inspired by MonoGame.ImGuiNet (https://github.com/tsMezotic/MonoGame.ImGuiNet)
    /// and the ImGui.NET XNA sample renderer.
    /// </summary>
    public class ImPlotRenderer : IDisposable
    {
        // ── GraphicsDevice ────────────────────────────────────────────────────
        private readonly GraphicsDevice _graphicsDevice;

        // ── ImGui context ─────────────────────────────────────────────────────
        /// <summary>
        /// The native ImGui context created by this renderer.
        /// Pass to <c>ImPlot.SetImGuiContext(ImGuiContext)</c> right after
        /// <c>ImPlot.CreateContext()</c> to link the two libraries.
        /// </summary>
        public ImGuiContextPtr ImGuiContext { get; }

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
        // Keys are sequential nint values used as ImTextureID handles.
        private readonly Dictionary<nint, Texture2D> _loadedTextures = new();
        private nint _textureId = 1;  // 0 == ImTextureID_Invalid in ImGui 1.92; never use 0 as a texture key

        // ── Input state ───────────────────────────────────────────────────────
        private int _scrollWheelValue;
        private int _horizontalScrollWheelValue;
        private const float WheelDelta = 120f;
        private readonly Keys[] _allKeys = Enum.GetValues<Keys>();

        // ─────────────────────────────────────────────────────────────────────
        // Construction / initialisation
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the renderer and an ImGui context.
        /// </summary>
        /// <param name="graphicsDevice">MonoGame GraphicsDevice.</param>
        /// <param name="window">GameWindow — used for text-input events.</param>
        public ImPlotRenderer(GraphicsDevice graphicsDevice, GameWindow window)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

            ImGuiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(ImGuiContext);

            // Tell ImGui that this backend handles texture lifecycle via ImDrawData.Textures.
            // This replaces the old GetTexDataAsRGBA32 / SetTexID font-atlas API (removed in 1.92+).
            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

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

        /// <summary>Convenience overload accepting a <see cref="Game"/>.</summary>
        public ImPlotRenderer(Game game)
            : this(game?.GraphicsDevice!, game?.Window!) { }

        /// <summary>
        /// Kept for API compatibility. With <c>ImGuiBackendFlags.RendererHasTextures</c>
        /// the font atlas is built and uploaded automatically on the first frame.
        /// Call this after loading custom fonts into <c>ImGui.GetIO().Fonts</c> if
        /// you want to trigger a rebuild before the first frame.
        /// </summary>
        public virtual void Initialize()
        {
            // Nothing to do — font atlas texture creation is driven by the
            // ImDrawData.Textures WantCreate/WantDestroy lifecycle below.
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Kept for API compatibility. With the RendererHasTextures backend flag the
        /// font atlas is rebuilt automatically by ImGui when fonts change. Call after
        /// adding custom fonts to request an immediate rebuild on the next frame.
        /// </summary>
        public virtual void RebuildFontAtlas()
        {
            // ImGui 1.92+ (Hexa.NET.ImGui 2.2+): GetTexDataAsRGBA32 / SetTexID are removed.
            // The backend flag ImGuiBackendFlags.RendererHasTextures causes ImGui to
            // manage the font atlas texture via ImDrawData.Textures automatically.
        }

        /// <summary>
        /// Registers a MonoGame texture with ImGui and returns a handle
        /// for use with <c>ImGui.Image()</c> etc.
        /// </summary>
        public virtual nint BindTexture(Texture2D texture)
        {
            var id = _textureId++;
            _loadedTextures.Add(id, texture);
            return id;
        }

        /// <summary>Removes a previously bound texture.</summary>
        public virtual void UnbindTexture(nint textureId)
        {
            _loadedTextures.Remove(textureId);
        }

        /// <summary>
        /// Call at the start of your Draw method.
        /// Updates ImGui IO and calls <c>ImGui.NewFrame()</c>.
        /// </summary>
        public virtual void BeforeLayout(GameTime gameTime)
        {
            ImGui.GetIO().DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateInput();
            ImGui.NewFrame();
        }

        /// <summary>
        /// Call at the end of your Draw method, after all ImGui/ImPlot calls.
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
                io.AddInputCharacter((uint)a.Character);
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
                    => ImGuiKey.A - 10 + (key - Keys.D0),  // ImGuiKey_0 = ImGuiKey_A − 10
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
            _effect.World              = Matrix.Identity;
            _effect.View               = Matrix.Identity;
            _effect.Projection         = Matrix.CreateOrthographicOffCenter(
                0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
            _effect.TextureEnabled     = true;
            _effect.Texture            = texture;
            _effect.VertexColorEnabled = true;

            return _effect;
        }

        /// <summary>
        /// Processes a single <see cref="ImTextureDataPtr"/> entry from
        /// <c>ImDrawData.Textures</c>. Handles WantCreate, WantUpdates, and WantDestroy.
        /// </summary>
        private unsafe void UpdateTexture(ImTextureDataPtr tex)
        {
            if (tex.Status == ImTextureStatus.Ok)
                return;

            if (tex.Status == ImTextureStatus.WantCreate)
            {
                int sizeInBytes = tex.Width * tex.Height * tex.BytesPerPixel;
                var pixels = new byte[sizeInBytes];
                Marshal.Copy((IntPtr)tex.GetPixels(), pixels, 0, pixels.Length);

                var texture = new Texture2D(_graphicsDevice, tex.Width, tex.Height, false, SurfaceFormat.Color);
                texture.SetData(pixels);

                var id = _textureId++;
                _loadedTextures.Add(id, texture);

                tex.SetTexID(Unsafe.BitCast<ulong, ImTextureID>((ulong)(nint)id));
                tex.SetStatus(ImTextureStatus.Ok);
            }
            else if (tex.Status == ImTextureStatus.WantUpdates)
            {
                // Full re-upload on any atlas update (e.g. new glyphs loaded at runtime).
                var texKey = (nint)Unsafe.BitCast<ImTextureID, ulong>(tex.GetTexID());
                if (_loadedTextures.TryGetValue(texKey, out var existingTexture))
                {
                    int sizeInBytes = tex.Width * tex.Height * tex.BytesPerPixel;
                    var pixels = new byte[sizeInBytes];
                    Marshal.Copy((IntPtr)tex.GetPixels(), pixels, 0, pixels.Length);
                    existingTexture.SetData(pixels);
                }
                tex.SetStatus(ImTextureStatus.Ok);
            }
            else if (tex.Status == ImTextureStatus.WantDestroy)
            {
                var texKey = (nint)Unsafe.BitCast<ImTextureID, ulong>(tex.GetTexID());
                if (_loadedTextures.TryGetValue(texKey, out var existingTexture))
                {
                    existingTexture.Dispose();
                    _loadedTextures.Remove(texKey);
                }
                tex.SetTexID(default);
                tex.SetStatus(ImTextureStatus.Destroyed);
            }
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

            _graphicsDevice.BlendFactor       = Color.White;
            _graphicsDevice.BlendState        = BlendState.NonPremultiplied;
            _graphicsDevice.RasterizerState   = _rasterizerState;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;

            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            _graphicsDevice.Viewport = new Viewport(
                0, 0,
                _graphicsDevice.PresentationParameters.BackBufferWidth,
                _graphicsDevice.PresentationParameters.BackBufferHeight);

            // Process any pending texture creates / updates / destroys.
            // Use PlatformIO.Textures (inline ImVector) rather than drawData.Textures
            // (which is a C++ pointer — unsafe to dereference via the C# ptr-wrapper).
            var platformTextures = ImGui.GetPlatformIO().Textures;
            for (int i = 0; i < platformTextures.Size; i++)
                UpdateTexture(platformTextures[i]);

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

            if (drawData.TotalVtxCount > _vertexBufferSize)
            {
                _vertexBuffer?.Dispose();
                _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
                _vertexBuffer     = new VertexBuffer(
                    _graphicsDevice, DrawVertDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
                _vertexData = new byte[_vertexBufferSize * DrawVertDeclaration.Size];
            }

            if (drawData.TotalIdxCount > _indexBufferSize)
            {
                _indexBuffer?.Dispose();
                _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
                _indexBuffer     = new IndexBuffer(
                    _graphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
                _indexData = new byte[_indexBufferSize * sizeof(ushort)];
            }

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

        private unsafe void RenderCommandLists(ImDrawDataPtr drawData)
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

                    // Recover our nint key from the ImTextureID stored in the draw command.
                    // cmd.GetTexID() resolves through ImTextureRef (handles both direct IDs
                    // and backend-managed ImTextureData pointers).
                    var texKey = (nint)Unsafe.BitCast<ImTextureID, ulong>(cmd.GetTexID());
                    if (!_loadedTextures.TryGetValue(texKey, out var texture))
                        continue;  // texture not ready yet (e.g. first-frame WantCreate race); skip

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
                            primitiveType:  PrimitiveType.TriangleList,
                            baseVertex:     (int)cmd.VtxOffset + vtxOffset,
                            minVertexIndex: 0,
                            numVertices:    cmdList.VtxBuffer.Size,
                            startIndex:     (int)cmd.IdxOffset + idxOffset,
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

            _effect?.Dispose();
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _rasterizerState.Dispose();
        }
    }
}
