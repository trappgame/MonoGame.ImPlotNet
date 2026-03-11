using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

// If you have added an ImPlot NuGet package (e.g. Hexa.NET.ImPlot or Twizzle.ImPlot.NET),
// uncomment the appropriate using statement below and the ImPlot calls in this file.
//
// using ImPlotNET;        // Twizzle.ImPlot.NET
// using Hexa.NET.ImPlot;  // Hexa.NET.ImPlot

namespace MonoGame.ImPlot.Sample
{
    public class SampleGame : Game
    {
        private GraphicsDeviceManager _graphics;
        private ImPlotRenderer _imPlotRenderer = null!;

        // Sample data for the ImPlot demo (used when ImPlot is enabled)
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

            // Build sample sine-wave data
            for (int i = 0; i < _plotX.Length; i++)
            {
                _plotX[i] = i * 0.1;
                _plotY[i] = Math.Sin(_plotX[i]);
            }
        }

        protected override void LoadContent()
        {
            // 1. Create the renderer (also creates the ImGui context internally)
            _imPlotRenderer = new ImPlotRenderer(GraphicsDevice, Window);

            // 2. Optionally load custom fonts here before Initialize():
            //    var io = ImGui.GetIO();
            //    io.Fonts.AddFontFromFileTTF("path/to/font.ttf", 16);

            // 3. Build the font atlas
            _imPlotRenderer.Initialize();

            // 4. Create the ImPlot context AFTER ImGui context (handled inside the constructor above).
            //    Uncomment the line below once you have added an ImPlot.NET NuGet package:
            //
            //    ImPlot.CreateContext();   // <-- uncomment when using ImPlot
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
            // Uncomment the block below once you have added an ImPlot.NET package
            // and called ImPlot.CreateContext() in LoadContent().
            //
            // ImGui.Begin("ImPlot Demo");
            // if (ImPlot.BeginPlot("Sine Wave"))
            // {
            //     ImPlot.SetupAxes("x", "sin(x)");
            //     ImPlot.PlotLine("sin", ref _plotX[0], ref _plotY[0], _plotX.Length);
            //     ImPlot.EndPlot();
            // }
            // ImGui.End();

            // ── Custom window example ───────────────────────────────────────
            ImGui.Begin("MonoGame.ImPlot");
            ImGui.Text("Hello from MonoGame.ImPlot!");
            ImGui.Separator();
            ImGui.TextWrapped(
                "Add an ImPlot.NET NuGet package to your project (e.g. Hexa.NET.ImPlot " +
                "or Twizzle.ImPlot.NET), call ImPlot.CreateContext() in LoadContent(), " +
                "then uncomment the ImPlot demo block in Draw().");
            ImGui.End();

            // ── ImGui frame end (renders everything) ───────────────────────
            _imPlotRenderer.AfterLayout();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            // Destroy ImPlot context BEFORE the renderer (which destroys ImGui context).
            // Uncomment when using ImPlot:
            //
            // ImPlot.DestroyContext();   // <-- uncomment when using ImPlot

            _imPlotRenderer.Dispose();
            base.UnloadContent();
        }
    }
}
