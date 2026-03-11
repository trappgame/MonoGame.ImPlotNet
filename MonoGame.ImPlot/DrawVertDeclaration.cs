using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

namespace MonoGame.ImPlotNet
{
    /// <summary>
    /// MonoGame VertexDeclaration matching ImGui's ImDrawVert layout:
    ///   float2 pos   (offset 0,  8 bytes)
    ///   float2 uv    (offset 8,  8 bytes)
    ///   uint   col   (offset 16, 4 bytes)
    ///   total        20 bytes
    /// </summary>
    public static class DrawVertDeclaration
    {
        public static readonly int Size = Marshal.SizeOf<ImDrawVert>();

        public static readonly VertexDeclaration Declaration = new VertexDeclaration(
            Size,
            new VertexElement(0,  VertexElementFormat.Vector2, VertexElementUsage.Position,          0),
            new VertexElement(8,  VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(16, VertexElementFormat.Color,   VertexElementUsage.Color,             0)
        );
    }
}
