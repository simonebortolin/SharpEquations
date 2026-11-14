namespace SharpEquations.Avalonia.MathTools
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Input;
    using global::Avalonia.Media;
    using global::Avalonia.Media.Imaging;
    using global::Avalonia.Media.Immutable;
    using global::Avalonia.OpenGL;
    using global::Avalonia.OpenGL.Controls;
    using global::Avalonia.Threading;
    using SharpEquations.MathTools;
    using SharpEquations.MathTools.Graphing;
    using Silk.NET.OpenGL;

    /// <summary>A graph in space drawn by <see cref="Graph3DView"/>: the plot of an equation and its color.</summary>
    public sealed record SurfaceGraph(Equation Equation, SurfacePlot Plot, Color Color);

    /// <summary>
    /// Surfaces z=f(x,y) and curves of t in a box, seen by a camera that turns around it: dragged to turn, the wheel to
    /// zoom the region of x and y, a double click to start again. OpenGL draws them when the platform has it; otherwise,
    /// and until it starts, Skia does, sorting the triangles from the farthest to the nearest.
    /// </summary>
    public sealed class Graph3DView : Panel
    {
        private const int GlGrid = 80;
        private const int SoftwareGrid = 40;

        private readonly SoftwareCanvas canvas;
        private readonly GlSurface surface;
        private readonly Overlay overlay;
        private OrbitCamera camera = OrbitCamera.Default;
        private Viewport domain = Viewport.Default;
        private Point? dragged;
        private bool forceSoftware;
        private DispatcherTimer? pending;

        public Graph3DView()
        {
            ClipToBounds = true;
            Focusable = true;
            Bind(BackgroundProperty, this.GetResourceObservable("GraphBrush"));
            canvas = new SoftwareCanvas(this) { IsHitTestVisible = false };
            surface = new GlSurface(this) { IsHitTestVisible = false };
            overlay = new Overlay(this) { IsHitTestVisible = false };
            Children.Add(surface);
            Children.Add(canvas);
            Children.Add(overlay);
            Surfaces.CollectionChanged += (_, _) => Redraw();
            ActualThemeVariantChanged += (_, _) => Invalidate();
        }

        /// <summary>The graphs shown.</summary>
        public ObservableCollection<SurfaceGraph> Surfaces { get; } = new ObservableCollection<SurfaceGraph>();

        /// <summary>What the graphs are drawn with now: "OpenGL" and its version, or "Software (Skia)".</summary>
        public string Renderer { get; private set; } = "Software (Skia)";

        /// <summary>True when OpenGL draws the graphs.</summary>
        public bool IsOpenGl { get; private set; }

        /// <summary>Draws with Skia even where OpenGL is there.</summary>
        public bool ForceSoftware
        {
            get => forceSoftware;
            set
            {
                forceSoftware = value;
                if (value)
                    UseSoftware();
                else
                    surface.IsVisible = true;
            }
        }

        /// <summary>The camera, around the box −1..1 the graphs are placed in.</summary>
        public OrbitCamera Camera
        {
            get => camera;
            set
            {
                camera = value;
                Invalidate();
            }
        }

        /// <summary>The region of x and y shown; the heights follow from what the surfaces reach.</summary>
        public Viewport Domain
        {
            get => domain;
            set
            {
                domain = value;
                Redraw();
            }
        }

        /// <summary>What is drawn: sampled again by <see cref="Redraw"/>.</summary>
        public Scene3D Scene { get; private set; } = Scene3D.Empty;

        /// <summary>The camera and the region of a new graph.</summary>
        public void Reset()
        {
            camera = OrbitCamera.Default;
            Domain = Viewport.Default;
        }

        /// <summary>
        /// Samples the graphs again once <paramref name="delay"/> (200 ms by default) passed without another call: a
        /// slider moving redraws when it stops, not at each of its steps.
        /// </summary>
        public void RedrawSoon(TimeSpan? delay = null)
        {
            if (pending is null)
            {
                pending = new DispatcherTimer();
                pending.Tick += (_, _) => Redraw();
            }
            pending.Stop();
            pending.Interval = delay ?? TimeSpan.FromMilliseconds(200);
            pending.Start();
        }

        /// <summary>True while a <see cref="RedrawSoon"/> waits.</summary>
        public bool IsRedrawPending => pending?.IsEnabled == true;

        /// <summary>
        /// The graphs and the axes as an image, at <paramref name="scale"/> pixels for a pixel of the control, drawn by
        /// Skia whatever draws them on screen.
        /// </summary>
        public RenderTargetBitmap Snapshot(double scale = 2)
        {
            var size = Bounds.Size;
            var bitmap = new RenderTargetBitmap(
                new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scale)), Math.Max(1, (int)Math.Ceiling(size.Height * scale))),
                new global::Avalonia.Vector(96 * scale, 96 * scale));
            using (var context = bitmap.CreateDrawingContext())
            {
                if (Background is { } background)
                    context.FillRectangle(background, new Rect(size));
                SoftwareCanvas.Paint(this, context, size);
                Overlay.Paint(this, context, size, renderer: false);
            }
            return bitmap;
        }

        /// <summary>Samples the graphs again, after a parameter changed, and draws them.</summary>
        public void Redraw()
        {
            pending?.Stop();
            var grid = IsOpenGl ? GlGrid : SoftwareGrid;
            var surfaces = new List<(Func<double, double, double>, Vector4)>();
            var curves = new List<(Func<double, (double, double, double)>, double, double, Vector4)>();
            foreach (var graph in Surfaces)
            {
                var plot = graph.Plot;
                var color = ToVector(graph.Color);
                if (plot.Kind == SurfaceKind.Surface)
                    surfaces.Add((plot.Z, color));
                else
                    curves.Add((plot.Point, plot.Range.Min, plot.Range.Max, color));
            }
            Scene = Scene3D.Build(surfaces, curves, domain, grid);
            surface.SceneChanged();
            Invalidate();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
            {
                Reset();
                return;
            }
            dragged = e.GetPosition(this);
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (dragged is not { } from)
                return;
            var position = e.GetPosition(this);
            Camera = camera.Rotate(-(float)(position.X - from.X) * 0.01f, (float)(position.Y - from.Y) * 0.01f);
            dragged = position;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            dragged = null;
            e.Pointer.Capture(null);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            Domain = domain.Zoom(Math.Pow(0.85, e.Delta.Y));
            e.Handled = true;
        }

        private void Invalidate()
        {
            canvas.InvalidateVisual();
            overlay.InvalidateVisual();
            surface.RequestNextFrameRendering();
        }

        /// <summary>OpenGL started: it draws from now on, on a finer grid.</summary>
        private void UseOpenGl(string version)
        {
            if (forceSoftware)
                return;
            IsOpenGl = true;
            Renderer = "OpenGL " + version;
            canvas.IsVisible = false;
            Redraw();
        }

        /// <summary>OpenGL failed, or was lost, or is not wanted: Skia draws from now on.</summary>
        private void UseSoftware()
        {
            var was = IsOpenGl;
            IsOpenGl = false;
            Renderer = "Software (Skia)";
            surface.IsVisible = false;
            canvas.IsVisible = true;
            if (was)
                Redraw();
            else
                Invalidate();
        }

        private IBrush Brush(string key, IBrush fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;

        private Vector4 BrushColor(string key, Color fallback) =>
            ToVector(Brush(key, Brushes.Transparent) is ISolidColorBrush solid ? solid.Color : fallback);

        private static Vector4 ToVector(Color c) => new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

        private static Color ToColor(Vector4 v) => Color.FromArgb(
            (byte)Math.Clamp(v.W * 255, 0, 255), (byte)Math.Clamp(v.X * 255, 0, 255), (byte)Math.Clamp(v.Y * 255, 0, 255), (byte)Math.Clamp(v.Z * 255, 0, 255));

        private static FormattedText Text(string text, double size, IBrush brush) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

        private static string Number(double value) =>
            Math.Abs(value) < 1e-12 ? "0" : value.ToString(Math.Abs(value) >= 1e5 || Math.Abs(value) < 1e-3 ? "0.###E0" : "0.###", CultureInfo.InvariantCulture);

        /// <summary>The graphs drawn by Skia: the painter's triangles and segments, in order.</summary>
        private sealed class SoftwareCanvas : Control
        {
            private readonly Graph3DView owner;

            public SoftwareCanvas(Graph3DView owner) => this.owner = owner;

            public override void Render(DrawingContext context) => Paint(owner, context, Bounds.Size);

            public static void Paint(Graph3DView owner, DrawingContext context, Size size)
            {
                var edges = owner.BrushColor("AxisBrush", Colors.Gray);
                var shapes = Painter3D.Paint(owner.Scene, owner.camera, (float)size.Width, (float)size.Height, edges);
                foreach (var shape in shapes)
                {
                    var brush = new ImmutableSolidColorBrush(ToColor(shape.Color));
                    if (shape.IsTriangle)
                    {
                        var geometry = new StreamGeometry();
                        using (var g = geometry.Open())
                        {
                            g.BeginFigure(new Point(shape.A.X, shape.A.Y), true);
                            g.LineTo(new Point(shape.B.X, shape.B.Y));
                            g.LineTo(new Point(shape.C.X, shape.C.Y));
                            g.EndFigure(true);
                        }
                        // a thin border of the same color closes the seams between the triangles
                        context.DrawGeometry(brush, new Pen(brush, 0.5), geometry);
                    }
                    else
                    {
                        context.DrawLine(new Pen(brush, 1.6, lineCap: PenLineCap.Round), new Point(shape.A.X, shape.A.Y), new Point(shape.B.X, shape.B.Y));
                    }
                }
            }
        }

        /// <summary>The names and the ends of the axes, on the edges of the box facing the camera, and what draws.</summary>
        private sealed class Overlay : Control
        {
            private readonly Graph3DView owner;

            public Overlay(Graph3DView owner) => this.owner = owner;

            public override void Render(DrawingContext context) => Paint(owner, context, Bounds.Size, renderer: true);

            /// <param name="renderer">Whether to write what draws the graphs, in the corner.</param>
            public static void Paint(Graph3DView owner, DrawingContext context, Size size, bool renderer)
            {
                if (size.Width < 2 || size.Height < 2)
                    return;
                var brush = owner.Brush("AxisBrush", Brushes.Gray);
                var camera = owner.camera;
                var box = owner.Scene.Box;
                var eye = camera.Eye;
                var sx = eye.X >= 0 ? 1f : -1f;
                var sy = eye.Y >= 0 ? 1f : -1f;
                var center = new Vector2((float)size.Width / 2, (float)size.Height / 2);

                Axis("x", new Vector3(-1, sy, -1), new Vector3(1, sy, -1), box.XMin, box.XMax);
                Axis("y", new Vector3(sx, -1, -1), new Vector3(sx, 1, -1), box.YMin, box.YMax);
                Axis("z", new Vector3(sx, -sy, -1), new Vector3(sx, -sy, 1), box.ZMin, box.ZMax);

                if (renderer)
                {
                    var drawn = Text(owner.Renderer, 11, brush);
                    context.DrawText(drawn, new Point(size.Width - drawn.Width - 10, size.Height - drawn.Height - 8));
                }

                void Axis(string name, Vector3 a, Vector3 b, double min, double max)
                {
                    // a little inside the edge, so that the ends of two axes meeting at a corner do not cover each other
                    Label(Number(min), Vector3.Lerp(a, b, 0.1f), 11);
                    Label(Number(max), Vector3.Lerp(a, b, 0.9f), 11);
                    Label(name, (a + b) / 2, 14);
                }

                void Label(string text, Vector3 point, double fontSize)
                {
                    if (!camera.ToScreen(point, (float)size.Width, (float)size.Height, out var p, out _))
                        return;
                    var away = p - center;
                    p += away == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(away) * 16;
                    var formatted = Text(text, fontSize, brush);
                    context.DrawText(formatted, new Point(p.X - formatted.Width / 2, p.Y - formatted.Height / 2));
                }
            }
        }

        /// <summary>
        /// The graphs drawn by OpenGL (through Silk.NET): lit triangles and lines, with a depth buffer. GLSL 1.50 or
        /// 3.00 ES where there are vertex arrays, 1.20 or 1.00 ES otherwise; any failure hands the drawing back to Skia.
        /// </summary>
        private sealed class GlSurface : OpenGlControlBase
        {
            private const int Floats = 10; // position, normal, color
            private readonly Graph3DView owner;
            private GL? gl;
            private uint program;
            private uint vao;
            private uint triangles;
            private uint lines;
            private int triangleCount;
            private int lineCount;
            private int mvp;
            private int light;
            private int lit;
            private bool modern;
            private bool dirty = true;

            public GlSurface(Graph3DView owner) => this.owner = owner;

            public void SceneChanged()
            {
                dirty = true;
                RequestNextFrameRendering();
            }

            protected override void OnOpenGlInit(GlInterface glInterface)
            {
                base.OnOpenGlInit(glInterface);
#pragma warning disable CA1031 // whatever stops OpenGL, Skia draws
                try
                {
                    Init(glInterface);
                    var version = GlVersion;
                    var name = (version.Type == GlProfileType.OpenGLES ? "ES " : "") + version.Major + "." + version.Minor;
                    Dispatcher.UIThread.Post(() => owner.UseOpenGl(name));
                }
                catch (Exception)
                {
                    gl = null;
                    Dispatcher.UIThread.Post(owner.UseSoftware);
                }
#pragma warning restore CA1031
            }

            protected override void OnOpenGlDeinit(GlInterface glInterface)
            {
                if (gl is { } g)
                {
                    g.DeleteBuffer(triangles);
                    g.DeleteBuffer(lines);
                    if (vao != 0)
                        g.DeleteVertexArray(vao);
                    g.DeleteProgram(program);
                }
                gl = null;
                dirty = true;
                base.OnOpenGlDeinit(glInterface);
            }

            protected override void OnOpenGlLost()
            {
                gl = null;
                base.OnOpenGlLost();
                Dispatcher.UIThread.Post(owner.UseSoftware);
            }

            protected override unsafe void OnOpenGlRender(GlInterface glInterface, int fb)
            {
                if (gl is not { } g)
                    return;
                var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
                var width = (int)Math.Max(1, Bounds.Width * scale);
                var height = (int)Math.Max(1, Bounds.Height * scale);
                g.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
                g.Viewport(0, 0, (uint)width, (uint)height);
                var back = owner.BrushColor("GraphBrush", Colors.White);
                g.ClearColor(back.X, back.Y, back.Z, 1);
                g.ClearDepth(1);
                g.Enable(EnableCap.DepthTest);
                g.DepthFunc(DepthFunction.Lequal);
                g.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                if (dirty)
                    Upload(g);

                g.UseProgram(program);
                if (modern)
                    g.BindVertexArray(vao);
                var matrix = owner.camera.ViewProjection((float)(Bounds.Width / Math.Max(1, Bounds.Height)));
                g.UniformMatrix4(mvp, 1, false, (float*)&matrix);
                var l = Painter3D.Light(owner.camera);
                g.Uniform3(light, l.X, l.Y, l.Z);

                // the surfaces a little farther than they are, so that the curves and edges on them are seen
                g.Enable(EnableCap.PolygonOffsetFill);
                g.PolygonOffset(1, 1);
                g.Uniform1(lit, 1f);
                Draw(g, triangles, PrimitiveType.Triangles, triangleCount);
                g.Disable(EnableCap.PolygonOffsetFill);
                g.Uniform1(lit, 0f);
                Draw(g, lines, PrimitiveType.Lines, lineCount);

                if (modern)
                    g.BindVertexArray(0);
                g.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
                g.UseProgram(0);
                g.Disable(EnableCap.DepthTest);
            }

            private void Init(GlInterface glInterface)
            {
                var g = GL.GetApi(glInterface.GetProcAddress);
                var version = GlVersion;
                var es = version.Type == GlProfileType.OpenGLES;
                modern = es ? version.Major >= 3 : version.Major > 3 || (version.Major == 3 && version.Minor >= 2);
                var header = (es, modern) switch
                {
                    (true, true) => "#version 300 es\nprecision mediump float;\n",
                    (true, false) => "#version 100\nprecision mediump float;\n",
                    (false, true) => "#version 150\n",
                    _ => "#version 120\n",
                };
                var (input, output) = modern ? ("in", "out") : ("attribute", "varying");
                var vertex = header +
                    input + " vec3 a_pos;\n" + input + " vec3 a_normal;\n" + input + " vec4 a_color;\n" +
                    output + " vec4 v_color;\n" +
                    "uniform mat4 u_mvp;\nuniform vec3 u_light;\nuniform float u_lit;\n" +
                    "void main() {\n" +
                    "  float shade = mix(1.0, 0.35 + 0.65 * abs(dot(normalize(a_normal + vec3(0.0, 0.0, 1e-6)), u_light)), u_lit);\n" +
                    "  v_color = vec4(a_color.rgb * shade, a_color.a);\n" +
                    "  gl_Position = u_mvp * vec4(a_pos, 1.0);\n" +
                    "}\n";
                var fragment = header + (modern
                    ? "in vec4 v_color;\nout vec4 color;\nvoid main() { color = v_color; }\n"
                    : "varying vec4 v_color;\nvoid main() { gl_FragColor = v_color; }\n");

                var vs = Compile(g, ShaderType.VertexShader, vertex);
                var fs = Compile(g, ShaderType.FragmentShader, fragment);
                program = g.CreateProgram();
                g.AttachShader(program, vs);
                g.AttachShader(program, fs);
                g.BindAttribLocation(program, 0, "a_pos");
                g.BindAttribLocation(program, 1, "a_normal");
                g.BindAttribLocation(program, 2, "a_color");
                g.LinkProgram(program);
                g.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
                g.DeleteShader(vs);
                g.DeleteShader(fs);
                if (linked == 0)
                    throw new InvalidOperationException(g.GetProgramInfoLog(program));
                mvp = g.GetUniformLocation(program, "u_mvp");
                light = g.GetUniformLocation(program, "u_light");
                lit = g.GetUniformLocation(program, "u_lit");
                if (modern)
                    vao = g.GenVertexArray();
                triangles = g.GenBuffer();
                lines = g.GenBuffer();
                if (g.GetError() != GLEnum.NoError)
                    throw new InvalidOperationException("OpenGL did not start.");
                gl = g;
                dirty = true;
            }

            private static uint Compile(GL g, ShaderType type, string source)
            {
                var shader = g.CreateShader(type);
                g.ShaderSource(shader, source);
                g.CompileShader(shader);
                g.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
                if (compiled == 0)
                    throw new InvalidOperationException(g.GetShaderInfoLog(shader));
                return shader;
            }

            /// <summary>The scene in two arrays of vertices, each triangle and each segment on its own (no indices).</summary>
            private void Upload(GL g)
            {
                var scene = owner.Scene;
                var tris = new List<float>();
                foreach (var mesh in scene.Surfaces)
                {
                    foreach (var i in mesh.Indices)
                        Vertex(tris, mesh.Positions[(int)i], mesh.Normals[(int)i], mesh.Colors[(int)i]);
                }
                var segs = new List<float>();
                foreach (var curve in scene.Curves)
                {
                    foreach (var piece in curve.Pieces)
                    {
                        for (var k = 1; k < piece.Count; k++)
                        {
                            Vertex(segs, piece[k - 1], Vector3.Zero, curve.Color);
                            Vertex(segs, piece[k], Vector3.Zero, curve.Color);
                        }
                    }
                }
                var edges = owner.BrushColor("AxisBrush", Colors.Gray);
                foreach (var (a, b) in Scene3D.Edges())
                {
                    Vertex(segs, a, Vector3.Zero, edges);
                    Vertex(segs, b, Vector3.Zero, edges);
                }
                g.BindBuffer(BufferTargetARB.ArrayBuffer, triangles);
                g.BufferData<float>(BufferTargetARB.ArrayBuffer, tris.ToArray(), BufferUsageARB.StaticDraw);
                g.BindBuffer(BufferTargetARB.ArrayBuffer, lines);
                g.BufferData<float>(BufferTargetARB.ArrayBuffer, segs.ToArray(), BufferUsageARB.StaticDraw);
                triangleCount = tris.Count / Floats;
                lineCount = segs.Count / Floats;
                dirty = false;
            }

            private static void Vertex(List<float> data, Vector3 p, Vector3 n, Vector4 c)
            {
                data.Add(p.X);
                data.Add(p.Y);
                data.Add(p.Z);
                data.Add(n.X);
                data.Add(n.Y);
                data.Add(n.Z);
                data.Add(c.X);
                data.Add(c.Y);
                data.Add(c.Z);
                data.Add(c.W);
            }

            private static unsafe void Draw(GL g, uint buffer, PrimitiveType mode, int count)
            {
                if (count == 0)
                    return;
                const uint stride = Floats * sizeof(float);
                g.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
                g.EnableVertexAttribArray(0);
                g.EnableVertexAttribArray(1);
                g.EnableVertexAttribArray(2);
                g.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
                g.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
                g.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
                g.DrawArrays(mode, 0, (uint)count);
                g.DisableVertexAttribArray(0);
                g.DisableVertexAttribArray(1);
                g.DisableVertexAttribArray(2);
            }
        }
    }
}
