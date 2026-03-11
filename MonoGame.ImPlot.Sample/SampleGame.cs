using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace MonoGame.ImPlotNet.Sample
{
    public class SampleGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private ImPlotRenderer _imPlotRenderer = null!;

        // Sample data for the sine-wave plot
        private readonly double[] _plotX = new double[100];
        private readonly double[] _plotY = new double[100];

        public SampleGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = 1280,
                PreferredBackBufferHeight = 720,
            };
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Window.Title = "MonoGame.ImPlot Sample";
        }

        protected override void Initialize()
        {
            base.Initialize();

            for (int i = 0; i < _plotX.Length; i++)
            {
                _plotX[i] = i * 0.1;
                _plotY[i] = Math.Sin(_plotX[i]);
            }
        }

        protected override void LoadContent()
        {
            // 1. Create the renderer (also creates the ImGui context).
            _imPlotRenderer = new ImPlotRenderer(GraphicsDevice, Window);

            // 2. Optionally load custom fonts before Initialize():
            //    ImGui.GetIO().Fonts.AddFontFromFileTTF("path/to/font.ttf", 16);

            // 3. Build the font atlas.
            _imPlotRenderer.Initialize();

            // 4. Create the ImPlot context and link it to the ImGui context.
            //    SetImGuiContext is required — omitting it causes AccessViolationException.
            ImPlot.CreateContext();
            ImPlot.SetImGuiContext(_imPlotRenderer.ImGuiContext);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // ── ImGui frame start ───────────────────────────────────────────
            _imPlotRenderer.BeforeLayout(gameTime);

            // ── ImGui demo window ───────────────────────────────────────────
            ImGui.ShowDemoWindow();

            // ── ImPlot demo window ──────────────────────────────────────────
            ImGui.Begin("ImPlot Demo");
            if (ImPlot.BeginPlot("Sine Wave"))
            {
                ImPlot.SetupAxes("x", "sin(x)");
                ImPlot.PlotLine("sin", ref _plotX[0], ref _plotY[0], _plotX.Length);
                ImPlot.EndPlot();
            }
            ImGui.End();

            // ── ImGui frame end (renders everything) ────────────────────────
            _imPlotRenderer.AfterLayout();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            // Destroy ImPlot context BEFORE the renderer (which destroys the ImGui context).
            ImPlot.DestroyContext();
            _imPlotRenderer.Dispose();
            base.UnloadContent();
        }
    }
}
