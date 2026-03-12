# MonoGame.ImPlotNet

A MonoGame renderer backend for [Dear ImGui](https://github.com/ocornut/imgui) and [ImPlot](https://github.com/epezent/implot), built on the [Hexa.NET.ImGui](https://github.com/HexaEngine/Hexa.NET.ImGui) / [Hexa.NET.ImPlot](https://www.nuget.org/packages/Hexa.NET.ImPlot) ecosystem.

Inspired by [MonoGame.ImGuiNet](https://github.com/tsMezotic/MonoGame.ImGuiNet).

---

## How it works

ImPlot renders entirely through ImGui's draw lists — the MonoGame renderer code is identical whether you use ImGui alone or together with ImPlot. `ImPlotRenderer` handles:

- ImGui context creation and the full render pipeline
- Font atlas texture upload to MonoGame's `GraphicsDevice`
- Mouse/keyboard input translation
- Dynamic vertex/index buffer management
- Graphics state save/restore

You manage the ImPlot context yourself (`ImPlot.CreateContext()` / `ImPlot.DestroyContext()`).

> **Why Hexa.NET?**
> `Hexa.NET.ImGui` and `Hexa.NET.ImPlot` ship the *same* native `cimgui` binaries and use matching C# types. Mixing `ImGui.NET` (mellinoe) with `Hexa.NET.ImPlot` causes DLL conflicts and `AccessViolationException`. Use one ecosystem consistently.

---

## Installation

### NuGet packages required

```xml
<PackageReference Include="Hexa.NET.ImGui"  Version="2.2.9" />
<PackageReference Include="Hexa.NET.ImPlot" Version="2.2.9" />
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.1.303" />
```

Then add a project (or NuGet) reference to `MonoGame.ImPlot`.

---

## Quick start

```csharp
using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using MonoGame.ImPlotNet;

public class MyGame : Game
{
    private ImPlotRenderer _renderer = null!;

    protected override void LoadContent()
    {
        // 1. Create renderer (creates the ImGui context internally).
        _renderer = new ImPlotRenderer(GraphicsDevice, Window);

        // 2. Optionally load custom fonts before Initialize():
        //    ImGui.GetIO().Fonts.AddFontFromFileTTF("font.ttf", 16);

        // 3. Build the font atlas.
        _renderer.Initialize();

        // 4. Create the ImPlot context and link it to the ImGui context.
        //    SetImGuiContext is required — omitting it causes AccessViolationException.
        ImPlot.CreateContext();
        ImPlot.SetImGuiContext(_renderer.ImGuiContext);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _renderer.BeforeLayout(gameTime);

        // ── Your ImGui / ImPlot calls go here ──────────────────────────────
        ImGui.Begin("My Window");
        if (ImPlot.BeginPlot("My Plot"))
        {
            double[] xs = { 0, 1, 2, 3, 4 };
            double[] ys = { 0, 1, 4, 9, 16 };
            ImPlot.PlotLine("x²", ref xs[0], ref ys[0], xs.Length);
            ImPlot.EndPlot();
        }
        ImGui.End();
        // ───────────────────────────────────────────────────────────────────

        _renderer.AfterLayout();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        ImPlot.DestroyContext();  // must come before renderer.Dispose()
        _renderer.Dispose();
        base.UnloadContent();
    }
}
```

---

## Binding textures

```csharp
// Register
nint handle = _renderer.BindTexture(myTexture2D);

// Use in ImGui
ImGui.Image((ulong)handle, new Vector2(myTexture2D.Width, myTexture2D.Height));

// Unregister when no longer needed
_renderer.UnbindTexture(handle);
```

---

## Project structure

```
MonoGame.ImPlotNet/
├── MonoGame.ImPlot/          ← Renderer library (Hexa.NET.ImGui + MonoGame)
│   ├── ImPlotRenderer.cs
│   └── DrawVertDeclaration.cs
└── MonoGame.ImPlot.Sample/   ← Demo app (Hexa.NET.ImGui + Hexa.NET.ImPlot)
    └── SampleGame.cs
```

---

## Requirements

- .NET 8.0
- MonoGame 3.8.1 (DesktopGL or WindowsDX)
- Hexa.NET.ImGui 2.0.1+
- Hexa.NET.ImPlot 2.0.1+
