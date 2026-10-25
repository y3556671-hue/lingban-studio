using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lingban
{
    /// <summary>A resolution-independent companion illustration; no assets or network required.</summary>
    public class PetView : FrameworkElement
    {
        private PetProfile profile;
        private string pose = "idle";
        private bool blink;
        private BitmapSource portrait;
        private string loadedImageKey;
        private bool imageFailed;

        public PetProfile Profile
        {
            get { return profile; }
            set { profile = value; LoadPortrait(); InvalidateVisual(); }
        }

        public string Pose
        {
            get { return pose; }
            set { pose = value ?? "idle"; InvalidateVisual(); }
        }

        public bool Blink
        {
            get { return blink; }
            set { if (blink != value) { blink = value; InvalidateVisual(); } }
        }

        public PetView()
        {
            SnapsToDevicePixels = false;
            Focusable = false;
            IsHitTestVisible = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(160, 180);
        }

        private static SolidColorBrush Brush(string hex)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static readonly Brush Fur = Brush("#ECDAC0");
        private static readonly Brush FurShadow = Brush("#D8C09E");
        private static readonly Brush RabbitFur = Brush("#EED8DF");
        private static readonly Brush RabbitShadow = Brush("#DAB9C4");
        private static readonly Brush BlobFur = Brush("#C5DFD0");
        private static readonly Brush BlobShadow = Brush("#A3C5B1");
        private static readonly Brush EarPink = Brush("#DCAAA8");
        private static readonly Brush Ink = Brush("#5B453D");
        private static readonly Brush Cheek = Brush("#E7A09A");
        private static readonly Brush Cream = Brush("#FFF5E4");
        private static readonly Brush Scarf = Brush("#A87999");
        private static readonly Brush ScarfDark = Brush("#8B5F7D");
        private static readonly Brush Accent = Brush("#9A76B4");
        private static readonly Brush Ground = Brush("#200E071C");
        private static readonly Brush PaleGround = Brush("#100E071C");
        private static readonly Pen FacePen = MakePen(Ink, 2.6);
        private static readonly Pen FinePen = MakePen(Ink, 1.6);

        private static Pen MakePen(Brush brush, double thickness)
        {
            Pen pen = new Pen(brush, thickness);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private void LoadPortrait()
        {
            if (profile == null || profile.Kind != PetKind.Image || string.IsNullOrWhiteSpace(profile.ImagePath))
            {
                portrait = null;
                loadedImageKey = null;
                imageFailed = false;
                return;
            }

            try
            {
                string path = Path.GetFullPath(profile.ImagePath);
                FileInfo file = new FileInfo(path);
                string key = path + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
                if (key == loadedImageKey) return;

                // OnLoad releases the source file immediately, allowing the user to replace it.
                BitmapImage image = new BitmapImage();
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 800;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                }
                portrait = image;
                loadedImageKey = key;
                imageFailed = false;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is NotSupportedException)
                    && !(ex is ArgumentException) && !(ex is System.Runtime.InteropServices.COMException)
                    && !(ex is System.Security.SecurityException) && !(ex is InvalidOperationException)
                    && !(ex is FileFormatException)) throw;
                portrait = null;
                loadedImageKey = null;
                imageFailed = true;
            }
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters parameters)
        {
            Point point = parameters.HitPoint;
            double sx = ActualWidth / 160.0;
            double sy = ActualHeight / 180.0;
            if (sx <= 0 || sy <= 0) return null;
            point = new Point(point.X / sx, point.Y / sy);
            Rect hit = profile != null && profile.Kind == PetKind.Image && portrait != null
                ? new Rect(14, 10, 132, 158) : new Rect(16, 14, 132, 151);
            return hit.Contains(point) ? new PointHitTestResult(this, parameters.HitPoint) : null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            dc.PushTransform(new ScaleTransform(ActualWidth / 160.0, ActualHeight / 180.0));
            dc.DrawEllipse(PaleGround, null, new Point(82, 165), 62, 9);
            dc.DrawEllipse(Ground, null, new Point(82, 165), 46, 5);
            if (profile != null && profile.Kind == PetKind.Image && portrait != null)
            {
                DrawPortrait(dc);
            }
            else
            {
                PetKind kind = profile == null ? PetKind.Cat : profile.Kind;
                if (kind == PetKind.Image) kind = PetKind.Blob;
                DrawCreature(dc, kind);
                if (imageFailed) DrawImageFallback(dc);
            }
            dc.Pop();
        }

        private void DrawPortrait(DrawingContext dc)
        {
            double scale = Math.Min(132.0 / portrait.PixelWidth, 150.0 / portrait.PixelHeight);
            double width = portrait.PixelWidth * scale;
            double height = portrait.PixelHeight * scale;
            Rect rect = new Rect(80 - width / 2, 161 - height, width, height);
            dc.DrawImage(portrait, rect);
            if (pose == "pet") DrawHeart(dc, new Point(139, 35), 8);
            else if (pose == "focus") DrawSparkle(dc, new Point(140, 31), 8);
        }

        private static Geometry Shape(Action<StreamGeometryContext> draw)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open()) draw(context);
            geometry.Freeze();
            return geometry;
        }

        private void DrawCreature(DrawingContext dc, PetKind kind)
        {
            bool rabbit = kind == PetKind.Rabbit;
            bool blob = kind == PetKind.Blob;
            Brush fur = rabbit ? RabbitFur : blob ? BlobFur : Fur;
            Brush shadow = rabbit ? RabbitShadow : blob ? BlobShadow : FurShadow;
            double headY = blob ? 64 : 44;

            if (!blob)
            {
                if (rabbit)
                {
                    dc.PushTransform(new RotateTransform(-14, 49, 56));
                    dc.DrawRoundedRectangle(shadow, null, new Rect(34, 10, 28, 76), 16, 16);
                    dc.DrawRoundedRectangle(fur, null, new Rect(35, 8, 24, 73), 14, 14);
                    dc.DrawRoundedRectangle(EarPink, null, new Rect(42, 17, 10, 49), 6, 6);
                    dc.Pop();
                    dc.PushTransform(new RotateTransform(20, 111, 57));
                    dc.DrawRoundedRectangle(shadow, null, new Rect(99, 10, 28, 76), 16, 16);
                    dc.DrawRoundedRectangle(fur, null, new Rect(100, 8, 24, 73), 14, 14);
                    dc.DrawRoundedRectangle(EarPink, null, new Rect(107, 17, 10, 49), 6, 6);
                    dc.Pop();
                    dc.DrawEllipse(shadow, null, new Point(28, 135), 17, 16);
                    dc.DrawEllipse(fur, null, new Point(25, 132), 15, 15);
                }
                else
                {
                    dc.DrawGeometry(fur, null, Shape(delegate(StreamGeometryContext c)
                    {
                        c.BeginFigure(new Point(31, 74), true, true);
                        c.BezierTo(new Point(26, 56), new Point(19, 21), new Point(31, 21), true, false);
                        c.BezierTo(new Point(44, 22), new Point(63, 44), new Point(64, 57), true, false);
                    }));
                    dc.DrawGeometry(EarPink, null, Shape(delegate(StreamGeometryContext c)
                    {
                        c.BeginFigure(new Point(34, 56), true, true);
                        c.LineTo(new Point(31, 31), true, false);
                        c.LineTo(new Point(51, 52), true, false);
                    }));
                    dc.DrawGeometry(fur, null, Shape(delegate(StreamGeometryContext c)
                    {
                        c.BeginFigure(new Point(97, 57), true, true);
                        c.BezierTo(new Point(102, 40), new Point(127, 14), new Point(134, 23), true, false);
                        c.BezierTo(new Point(139, 34), new Point(131, 64), new Point(129, 76), true, false);
                    }));
                    dc.DrawGeometry(EarPink, null, Shape(delegate(StreamGeometryContext c)
                    {
                        c.BeginFigure(new Point(111, 53), true, true);
                        c.LineTo(new Point(129, 31), true, false);
                        c.LineTo(new Point(127, 58), true, false);
                    }));
                    dc.DrawGeometry(shadow, null, Shape(delegate(StreamGeometryContext c)
                    {
                        c.BeginFigure(new Point(39, 130), true, true);
                        c.BezierTo(new Point(10, 143), new Point(1, 121), new Point(13, 113), true, false);
                        c.BezierTo(new Point(18, 108), new Point(28, 112), new Point(26, 118), true, false);
                        c.BezierTo(new Point(22, 123), new Point(35, 123), new Point(44, 116), true, false);
                    }));
                }
            }
            dc.DrawRoundedRectangle(shadow, null, new Rect(26, headY + 3, 112, 113 - (headY - 44)), 48, 48);
            dc.DrawRoundedRectangle(fur, null, new Rect(23, headY, 111, 110 - (headY - 44)), 47, 47);
            dc.DrawEllipse(shadow, null, new Point(48, 155), 18, 10);
            dc.DrawEllipse(shadow, null, new Point(113, 155), 18, 10);
            dc.DrawEllipse(fur, null, new Point(46, 153), 17, 9);
            dc.DrawEllipse(fur, null, new Point(111, 153), 17, 9);
            dc.DrawEllipse(Cream, null, new Point(80, 132), 23, 16);

            if (!blob && !rabbit)
            {
                dc.DrawGeometry(shadow, null, Shape(delegate(StreamGeometryContext c)
                {
                    c.BeginFigure(new Point(65, 45), true, true);
                    c.BezierTo(new Point(67, 65), new Point(72, 65), new Point(73, 44), true, false);
                }));
                dc.DrawGeometry(shadow, null, Shape(delegate(StreamGeometryContext c)
                {
                    c.BeginFigure(new Point(83, 44), true, true);
                    c.BezierTo(new Point(84, 63), new Point(89, 64), new Point(91, 45), true, false);
                }));
            }

            double eyeY = blob ? 94 : 82;
            bool eyesClosed = blink || pose == "pet" || pose == "sleep";
            bool spicy = profile != null && profile.Spice >= 60 && pose != "pet";
            if (eyesClosed)
            {
                DrawArc(dc, new Point(51, eyeY + 2), new Point(58, eyeY - 1), new Point(65, eyeY + 2), FacePen);
                DrawArc(dc, new Point(95, eyeY + 2), new Point(102, eyeY - 1), new Point(109, eyeY + 2), FacePen);
            }
            else if (spicy)
            {
                dc.DrawLine(FacePen, new Point(53, eyeY - 1), new Point(63, eyeY + 2));
                dc.DrawLine(FacePen, new Point(97, eyeY + 2), new Point(107, eyeY - 1));
            }
            else
            {
                dc.DrawEllipse(Ink, null, new Point(58, eyeY), 3.7, 6);
                dc.DrawEllipse(Ink, null, new Point(102, eyeY), 3.7, 6);
                dc.DrawEllipse(Cream, null, new Point(59, eyeY - 2), 1.1, 1.6);
                dc.DrawEllipse(Cream, null, new Point(103, eyeY - 2), 1.1, 1.6);
            }
            dc.PushOpacity(0.58);
            dc.DrawEllipse(Cheek, null, new Point(44, eyeY + 13), 10, 5.5);
            dc.DrawEllipse(Cheek, null, new Point(116, eyeY + 13), 10, 5.5);
            dc.Pop();

            dc.DrawEllipse(Ink, null, new Point(80, eyeY + 9), 2.5, 1.8);
            DrawArc(dc, new Point(80, eyeY + 10), new Point(77, eyeY + 20), new Point(72, eyeY + 14), FinePen);
            DrawArc(dc, new Point(80, eyeY + 10), new Point(83, eyeY + 20), new Point(88, eyeY + 14), FinePen);
            if (!blob)
            {
                dc.DrawRoundedRectangle(ScarfDark, null, new Rect(39, 117, 83, 13), 6, 6);
                dc.DrawRoundedRectangle(Scarf, null, new Rect(37, 114, 83, 12), 6, 6);
                dc.PushTransform(new RotateTransform(-12, 104, 126));
                dc.DrawRoundedRectangle(Scarf, null, new Rect(97, 121, 15, 28), 3, 3);
                dc.DrawLine(MakePen(ScarfDark, 2), new Point(100, 143), new Point(109, 143));
                dc.Pop();
            }
            else
            {
                dc.DrawGeometry(BlobShadow, null, Shape(delegate(StreamGeometryContext c)
                {
                    c.BeginFigure(new Point(76, 64), true, true);
                    c.BezierTo(new Point(65, 53), new Point(88, 43), new Point(94, 54), true, false);
                    c.BezierTo(new Point(89, 65), new Point(84, 67), new Point(76, 64), true, false);
                }));
            }
            if (pose == "pet") DrawHeart(dc, new Point(141, 42), 8);
            else if (pose == "focus" || pose == "hello") DrawSparkle(dc, new Point(143, 42), 8);
            else if (pose == "drag")
            {
                dc.DrawLine(MakePen(Accent, 3.3), new Point(145, 35), new Point(145, 46));
                dc.DrawEllipse(Accent, null, new Point(145, 52), 1.8, 1.8);
            }
        }

        private static void DrawArc(DrawingContext dc, Point start, Point control, Point end, Pen pen)
        {
            dc.DrawGeometry(null, pen, Shape(delegate(StreamGeometryContext c)
            {
                c.BeginFigure(start, false, false);
                c.QuadraticBezierTo(control, end, true, false);
            }));
        }

        private static void DrawHeart(DrawingContext dc, Point point, double size)
        {
            dc.DrawGeometry(Scarf, null, Shape(delegate(StreamGeometryContext c)
            {
                c.BeginFigure(new Point(point.X, point.Y + size), true, true);
                c.BezierTo(new Point(point.X - size * 2, point.Y - size / 4), new Point(point.X - size / 2, point.Y - size), point, true, false);
                c.BezierTo(new Point(point.X + size / 2, point.Y - size), new Point(point.X + size * 2, point.Y - size / 4), new Point(point.X, point.Y + size), true, false);
            }));
        }

        private static void DrawSparkle(DrawingContext dc, Point point, double size)
        {
            dc.DrawGeometry(Accent, null, Shape(delegate(StreamGeometryContext c)
            {
                c.BeginFigure(new Point(point.X, point.Y - size), true, true);
                c.LineTo(new Point(point.X + size * .3, point.Y - size * .3), true, false);
                c.LineTo(new Point(point.X + size, point.Y), true, false);
                c.LineTo(new Point(point.X + size * .3, point.Y + size * .3), true, false);
                c.LineTo(new Point(point.X, point.Y + size), true, false);
                c.LineTo(new Point(point.X - size * .3, point.Y + size * .3), true, false);
                c.LineTo(new Point(point.X - size, point.Y), true, false);
                c.LineTo(new Point(point.X - size * .3, point.Y - size * .3), true, false);
            }));
        }

        private static void DrawImageFallback(DrawingContext dc)
        {
            // A missing image remains a visible, usable pet rather than an empty window.
            DrawSparkle(dc, new Point(140, 35), 7);
        }
    }
}
