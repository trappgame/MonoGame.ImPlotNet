# MonoGame.ImPlotNet

A MonoGame renderer backend for [Dear ImGui](https://github.com/ocornut/imgui) (via [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET)) with first-class support for [ImPlot](https://github.com/epezent/implot) plotting widgets.

Inspired by [MonoGame.ImGuiNet](https://github.com/tsMezotic/MonoGame.ImGuiNet).

---

## How it works

ImPlot renders entirely through ImGui's draw lists — meaning the MonoGame renderer code is identical whether you use ImGui alone or together with ImPlot. `ImPlotRenderer` handles:

- ImGui context creation and the full render pipeline
- Font atlas texture upload to MonoGame's `GraphicsDevice`
- Mouse/keyboard input translation
- Dynamic vertex/index buffer management
- Graphics state save/restore

You call ImPlot context management yourself (`ImPlot.CreateContext()` / `ImPlot.DestroyContext()`), keeping the library free from any specific ImPlot NuGet binding.

---

## Installation

### 1. Add the library

Reference `MonoGame.ImPlot` in your project (NuGet — coming soon, or use a project reference).

### 2. Add ImGui.NET

```xml
<PackageReference Include="ImGui.NET" Version="1.91.6.1" />
```

### 3. Add an ImPlot.NET binding of your choice

| Package | NuGet ID |
|---|---|
| Hexa.NET.ImPlot (recommended) | `Hexa.NET.ImPlot` |
| Twizzle.ImPlot.NET | `Twizzle.ImPlot.NET` |

```xml
<!-- Example with Hexa.NET.ImPlot -->
<PackageReference Include="Hexa.NET.ImPlot" Version="2.0.1" />
```

---

## Quick start

```csharp
using ImGuiNET;
using ImPlotNET; // or Hexa.NET.ImPlot, etc.
using MonoGame.ImPlot;

public class MyGame : Game
{
    private ImPlotRenderer _renderer = null!;

    protected override void LoadContent()
    {
        // 1. Create the renderer (creates ImGui context internally)
        _renderer = new ImPlotRenderer(GraphicsDevice, Window);

        // 2. Optionally add custom fonts before Initialize()
        // ImGui.GetIO().Fonts.AddFontFromFileTTF("font.ttf", 16);

        // 3. Build the font atlas
        _renderer.Initialize();

        // 4. Create ImPlot context AFTER ImGui context
        ImPlot.CreateContext();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _renderer.BeforeLayout(gameTime);

        // --- your ImGui / ImPlot calls go here ---
        ImGui.Begin("My Window");
        if (ImPlot.BeginPlot("My Plot"))
        {
            double[] xs = { 0, 1, 2, 3, 4 };
            double[] ys = { 0, 1, 4, 9, 16 };
            ImPlot.PlotLine("x²", ref xs[0], ref ys[0], xs.Length);
            ImPlot.EndPlot();
        }
        ImGui.End();
        // -----------------------------------------

        _renderer.AfterLayout();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        ImPlot.DestroyContext(); // destroy ImPlot BEFORE ImGui
        _renderer.Dispose();
        base.UnloadContent();
    }
}
```

---

## Binding textures

To display MonoGame textures inside ImGui windows (e.g. `ImGui.Image()`):

```csharp
// Register
IntPtr handle = _renderer.BindTexture(myTexture2D);

// Use in ImGui
ImGui.Image(handle, new Vector2(myTexture2D.Width, myTexture2D.Height));

// Unregister when no longer needed
_renderer.UnbindTexture(handle);
```

---

## Project structure

```
MonoGame.ImPlotNet/
├── MonoGame.ImPlot/          ← Renderer library (no ImPlot package dependency)
│   ├── ImPlotRenderer.cs
│   └── DrawVertDeclaration.cs
└── MonoGame.ImPlot.Sample/   ← Demo app showing ImGui + ImPlot usage
    └── SampleGame.cs
```

---

## Requirements

- .NET 8.0
- MonoGame 3.8.1 (DesktopGL or WindowsDX)
- ImGui.NET 1.91.6+
- Any ImPlot.NET binding (optional — library works as a pure ImGui renderer too)
