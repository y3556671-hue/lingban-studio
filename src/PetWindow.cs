using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lingban
{
    /// <summary>The real transparent desktop companion window.</summary>
    public class PetWindow : Window
    {
        private PetProfile profile;
        private readonly PetView pet;
        private readonly Border bubble;
        private readonly TextBlock bubbleText;
        private readonly DispatcherTimer animation;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Random random = new Random();
        private readonly ScaleTransform breathing = new ScaleTransform(1, 1);
        private readonly RotateTransform tilt = new RotateTransform();
        private readonly TranslateTransform bounce = new TranslateTransform();
        private readonly ContextMenu menu;
        private MenuItem quietMenu;
        private double nextBlink;
        private double blinkUntil;
        private double poseUntil;
        private double bubbleUntil;
        private double nextIdleSpeech;
        private Point mouseDownScreen;
        private double dragLeft;
        private double dragTop;
        private bool pressed;
        private bool dragging;
        private bool shuttingDown;
        private string activePose = "idle";

        public event Action OpenStudioRequested;
        public event Action ExitRequested;
        public event Action ProfileChanged;
        public event Action<bool> CompanionVisibilityChanged;

        public PetWindow(PetProfile profile)
        {
            Title = "灵伴 · 桌面陪伴";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Width = 260;
            Height = 300;
            UseLayoutRounding = false;

            Grid root = new Grid();
            root.Background = null;
            Content = root;
            bubbleText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(72, 57, 86)),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
                FontSize = 12,
                MaxWidth = 206,
                TextAlignment = TextAlignment.Center
            };
            bubble = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 252, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 208, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14, 14, 14, 4),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 16, 10, 0),
                MaxWidth = 232,
                Visibility = Visibility.Collapsed,
                Child = bubbleText
            };
            pet = new PetView
            {
                Width = 176,
                Height = 198,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4),
                RenderTransformOrigin = new Point(.5, .93),
                ToolTip = "轻点摸头 · 拖动换位置 · 右键打开菜单"
            };
            TransformGroup transforms = new TransformGroup();
            transforms.Children.Add(breathing);
            transforms.Children.Add(tilt);
            transforms.Children.Add(bounce);
            pet.RenderTransform = transforms;
            root.Children.Add(pet);
            root.Children.Add(bubble);

            menu = BuildMenu();
            pet.ContextMenu = menu;
            bubble.ContextMenu = menu;
            pet.MouseLeftButtonDown += PetMouseDown;
            pet.MouseMove += PetMouseMove;
            pet.MouseLeftButtonUp += PetMouseUp;
            pet.LostMouseCapture += delegate { pressed = false; dragging = false; pet.Cursor = Cursors.Arrow; };
            bubble.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                bubble.Visibility = Visibility.Collapsed;
                e.Handled = true;
            };

            animation = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
            animation.Tick += AnimationTick;
            nextBlink = 3;
            nextIdleSpeech = 35;
            IsVisibleChanged += VisibilityChanged;
            Loaded += delegate { ClampToWorkArea(); };
            SourceInitialized += delegate { PlaceAtDefaultPosition(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!shuttingDown)
                {
                    e.Cancel = true;
                    SetCompanionVisible(false);
                }
            };
            Closed += delegate
            {
                animation.Stop();
                animation.Tick -= AnimationTick;
                clock.Stop();
            };
            ApplyProfile(profile);
        }

        private ContextMenu BuildMenu()
        {
            ContextMenu context = new ContextMenu();
            context.Items.Add(Menu("摸摸头", delegate { React("pet"); }));
            context.Items.Add(Menu("打个招呼", delegate { React("hello"); }));
            quietMenu = Menu("安静陪伴", delegate
            {
                if (profile == null) return;
                profile.Quiet = quietMenu.IsChecked;
                if (profile.Quiet) bubble.Visibility = Visibility.Collapsed;
                else React("hello");
                Action changed = ProfileChanged;
                if (changed != null) changed();
            });
            quietMenu.IsCheckable = true;
            context.Items.Add(quietMenu);
            context.Items.Add(new Separator());
            context.Items.Add(Menu("打开角色工坊", delegate
            {
                Action open = OpenStudioRequested;
                if (open != null) open();
            }));
            context.Items.Add(Menu("隐藏桌宠（可在工坊恢复）", delegate { SetCompanionVisible(false); }));
            context.Items.Add(new Separator());
            context.Items.Add(Menu("退出灵伴工坊", delegate
            {
                shuttingDown = true;
                animation.Stop();
                Action exit = ExitRequested;
                if (exit != null) exit();
                else if (Application.Current != null) Application.Current.Shutdown();
            }));
            return context;
        }

        private static MenuItem Menu(string title, Action action)
        {
            MenuItem item = new MenuItem { Header = title };
            item.Click += delegate { action(); };
            return item;
        }

        public void ApplyProfile(PetProfile updated)
        {
            if (updated == null) return;
            profile = updated;
            pet.Profile = profile;
            quietMenu.IsChecked = profile.Quiet;
            double scale = Math.Max(.6, Math.Min(1.8, profile.Size <= 0 ? 1 : profile.Size));
            pet.Width = 176 * scale;
            pet.Height = 198 * scale;
            Width = Math.Max(260, pet.Width + 60);
            Height = pet.Height + 102;
            if (profile.Quiet) bubble.Visibility = Visibility.Collapsed;
            if (IsLoaded) ClampToWorkArea();
        }

        public void React(string situation)
        {
            ReactWithText(situation, Personality.Respond(profile, situation));
        }

        public void ReactWithText(string situation, string response)
        {
            if (profile == null) return;
            activePose = situation ?? "idle";
            pet.Pose = activePose;
            double now = clock.Elapsed.TotalSeconds;
            poseUntil = now + (activePose == "drag" ? 2.0 : 4.5);
            if (!string.IsNullOrWhiteSpace(response))
            {
                // Quiet mode suppresses spontaneous dialogue, not deliberate user interaction.
                bubbleText.Text = response;
                bubble.Visibility = Visibility.Visible;
                bubbleUntil = now + 6.5;
            }
        }

        public void SetCompanionVisible(bool visible)
        {
            if (visible)
            {
                if (!IsVisible) Show();
                ClampToWorkArea();
            }
            else if (IsVisible) Hide();
        }

        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                clock.Start();
                animation.Start();
            }
            else
            {
                animation.Stop();
                clock.Stop();
                if (pressed) pet.ReleaseMouseCapture();
                bubble.Visibility = Visibility.Collapsed;
            }
            Action<bool> changed = CompanionVisibilityChanged;
            if (changed != null) changed(IsVisible);
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            if (profile == null || !IsVisible) return;
            double now = clock.Elapsed.TotalSeconds;
            double energy = Math.Max(0, Math.Min(100, profile.Energy));
            double affinity = Math.Max(0, Math.Min(100, profile.Affinity));
            if (now >= nextBlink)
            {
                blinkUntil = now + .2;
                nextBlink = now + 3 + random.NextDouble() * 4;
            }
            pet.Blink = now < blinkUntil;
            if (now >= poseUntil && !dragging)
            {
                activePose = "idle";
                pet.Pose = activePose;
            }
            if (bubble.Visibility == Visibility.Visible && now >= bubbleUntil)
                bubble.Visibility = Visibility.Collapsed;

            double breath = Math.Sin(now * (1.6 + energy / 100.0));
            breathing.ScaleX = 1 + .008 * breath;
            breathing.ScaleY = 1 - .012 * breath;
            if (!dragging)
            {
                double lively = energy / 100.0;
                tilt.Angle = activePose == "pet" ? 7 : activePose == "drag" ? -10 : Math.Sin(now * .75) * (1 + lively * 2);
                bounce.Y = activePose == "focus" ? -6 : -Math.Max(0, Math.Sin(now * 1.3)) * lively * 2.5;
            }
            else
            {
                tilt.Angle = -10;
                bounce.Y = -5;
            }

            if (now >= nextIdleSpeech)
            {
                nextIdleSpeech = now + 90 - affinity * .35 + random.NextDouble() * 35;
                if (!profile.Quiet && !dragging && !menu.IsOpen && bubble.Visibility != Visibility.Visible)
                {
                    bubbleText.Text = Personality.Respond(profile, "idle");
                    bubble.Visibility = Visibility.Visible;
                    bubbleUntil = now + 6;
                }
            }
        }

        private Point PointerScreen(MouseEventArgs e)
        {
            // PointToScreen returns physical pixels; convert motion with this window's DPI.
            return PointToScreen(e.GetPosition(this));
        }

        private void PetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || menu.IsOpen) return;
            pressed = true;
            dragging = false;
            mouseDownScreen = PointerScreen(e);
            dragLeft = Left;
            dragTop = Top;
            pet.CaptureMouse();
            e.Handled = true;
        }

        private void PetMouseMove(object sender, MouseEventArgs e)
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
            Point screen = PointerScreen(e);
            Vector physical = screen - mouseDownScreen;
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix inverse = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            Vector delta = inverse.Transform(physical);
            if (!dragging && (Math.Abs(delta.X) > Math.Max(5, SystemParameters.MinimumHorizontalDragDistance)
                || Math.Abs(delta.Y) > Math.Max(5, SystemParameters.MinimumVerticalDragDistance)))
            {
                dragging = true;
                pet.Cursor = Cursors.SizeAll;
                pet.Pose = "drag";
                bubble.Visibility = Visibility.Collapsed;
            }
            if (dragging)
            {
                Left = dragLeft + delta.X;
                Top = dragTop + delta.Y;
            }
            e.Handled = true;
        }

        private void PetMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!pressed || e.ChangedButton != MouseButton.Left) return;
            bool wasDragging = dragging;
            pressed = false;
            dragging = false;
            pet.ReleaseMouseCapture();
            pet.Cursor = Cursors.Arrow;
            if (wasDragging)
            {
                ClampToWorkArea();
                React("drag");
            }
            else React("pet");
            e.Handled = true;
        }

        private Rect CurrentWorkArea()
        {
            // Native monitor bounds are converted to WPF DIP coordinates using this HWND's DPI.
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                    PresentationSource source = PresentationSource.FromVisual(this);
                    Matrix inverse = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
                    Point topLeft = inverse.Transform(new Point(bounds.Left, bounds.Top));
                    Point bottomRight = inverse.Transform(new Point(bounds.Right, bounds.Bottom));
                    return new Rect(topLeft, bottomRight);
                }
            }
            catch (InvalidOperationException) { }
            return SystemParameters.WorkArea;
        }

        private void PlaceAtDefaultPosition()
        {
            Rect work = CurrentWorkArea();
            Left = work.Right - Width - 24;
            Top = work.Bottom - Height - 4;
            ClampToWorkArea();
        }

        private void ClampToWorkArea()
        {
            Rect work = CurrentWorkArea();
            if (double.IsNaN(Left) || double.IsInfinity(Left)) Left = work.Right - Width - 24;
            if (double.IsNaN(Top) || double.IsInfinity(Top)) Top = work.Bottom - Height - 4;
            Left = Math.Max(work.Left, Math.Min(Left, Math.Max(work.Left, work.Right - Width)));
            Top = Math.Max(work.Top, Math.Min(Top, Math.Max(work.Top, work.Bottom - Height)));
        }
    }
}
