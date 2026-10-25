using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Lingban
{
    public class StudioWindow : Window
    {
        private readonly PetProfile profile;
        private readonly PetWindow companion;
        private readonly PetView preview = new PetView { Width = 176, Height = 190 };
        private TextBox nameInput, mottoInput;
        private ComboBox kindInput;
        private CheckBox quietInput;
        private Slider energyInput, affinityInput, spiceInput, sizeInput;
        private TextBlock description, previewName, reply, status, imageStatus;
        private TextBlock energyValue, affinityValue, spiceValue, sizeValue;
        private Button visibilityButton;
        private Forms.NotifyIcon tray;
        private bool refreshing = true;
        private bool dirty;
        private bool exiting;
        private readonly Brush ink = ColorBrush("#342D44");
        private readonly Brush muted = ColorBrush("#7B728A");
        private readonly Brush accent = ColorBrush("#8167A9");

        public StudioWindow(PetProfile initial)
        {
            profile = initial;
            profile.Normalize();
            companion = new PetWindow(profile);
            Title = "灵伴工坊 · 本地体验版";
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13;
            Foreground = ink;
            Background = ColorBrush("#F5F2F8");
            Width = Math.Min(960, SystemParameters.WorkArea.Width - 24);
            Height = Math.Min(720, SystemParameters.WorkArea.Height - 24);
            MinWidth = 780;
            MinHeight = 570;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            BuildInterface();
            LoadFields();
            refreshing = false;
            RefreshPreview();
            status.Text = "本地体验版 · 所有角色设置保存在这个文件夹";
            companion.OpenStudioRequested += OpenStudio;
            companion.ExitRequested += ExitApplication;
            companion.ProfileChanged += delegate { LoadFields(); Changed(); };
            companion.CompanionVisibilityChanged += delegate(bool visible)
            {
                visibilityButton.Content = visible ? "隐藏桌宠" : "显示桌宠";
                if (!visible && !IsVisible && !exiting) OpenStudio();
            };
            Loaded += delegate
            {
                companion.SetCompanionVisible(true);
                companion.React("hello");
                CreateTray();
            };
            Closing += delegate { ExitApplication(); };
            Application.Current.Exit += delegate
            {
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
            };
        }

        private static Brush ColorBrush(string color)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private TextBlock Text(string value, double size, Brush color)
        {
            return new TextBlock { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap };
        }

        private Border Card(UIElement content, Thickness margin)
        {
            return new Border { Background = Brushes.White, BorderBrush = ColorBrush("#E5DEED"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(22), Margin = margin, Child = content };
        }

        private Button Button(string title, Action action, bool primary)
        {
            Button button = new Button
            {
                Content = title, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 8, 8),
                Background = primary ? accent : ColorBrush("#F3EFF8"), Foreground = primary ? Brushes.White : ink,
                BorderBrush = primary ? accent : ColorBrush("#E3DAED"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += delegate { action(); };
            return button;
        }

        private void BuildInterface()
        {
            Grid root = new Grid { Margin = new Thickness(24, 18, 24, 14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel titles = new StackPanel();
            titles.Children.Add(Text("灵伴工坊", 27, ink));
            titles.Children.Add(new TextBlock { Text = "把喜欢的角色，变成桌面上的小伙伴。", Foreground = muted, Margin = new Thickness(0, 5, 0, 0) });
            header.Children.Add(titles);
            Border badge = new Border { Background = ColorBrush("#EAE3F4"), CornerRadius = new CornerRadius(14), Padding = new Thickness(13, 6, 13, 6), VerticalAlignment = VerticalAlignment.Center, Child = Text("OFFLINE  /  0.1", 11, accent) };
            Grid.SetColumn(badge, 1); header.Children.Add(badge); root.Children.Add(header);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1); root.Children.Add(body);

            StackPanel settings = new StackPanel();
            settings.Children.Add(Text("01  创造你的角色", 18, ink));
            AddLabel(settings, "名字");
            nameInput = new TextBox { MaxLength = 20, Padding = new Thickness(10, 7, 10, 7) };
            AutomationProperties.SetName(nameInput, "角色名字");
            nameInput.TextChanged += delegate { if (!refreshing) { profile.Name = nameInput.Text; Changed(); } };
            settings.Children.Add(nameInput);
            AddLabel(settings, "形态");
            kindInput = new ComboBox { Padding = new Thickness(8, 6, 8, 6) };
            foreach (string kind in new[] { "小猫", "兔兔", "软团子", "我的图片" }) kindInput.Items.Add(kind);
            AutomationProperties.SetName(kindInput, "角色形态");
            kindInput.SelectionChanged += delegate
            {
                if (refreshing || kindInput.SelectedIndex < 0) return;
                if (kindInput.SelectedIndex == 3 && String.IsNullOrEmpty(profile.ImagePath))
                {
                    refreshing = true; kindInput.SelectedIndex = (int)profile.Kind; refreshing = false;
                    status.Text = "先点“导入角色图片”，再使用你的图片。"; return;
                }
                profile.Kind = (PetKind)kindInput.SelectedIndex; Changed();
            };
            settings.Children.Add(kindInput);
            WrapPanel importActions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            importActions.Children.Add(Button("导入角色图片", ImportImage, false)); settings.Children.Add(importActions);
            imageStatus = Text("PNG / JPG · 推荐透明背景 PNG", 11, muted); settings.Children.Add(imageStatus);
            settings.Children.Add(new Border { Height = 1, Background = ColorBrush("#EAE4F1"), Margin = new Thickness(0, 19, 0, 19) });
            settings.Children.Add(Text("02  给它一点自己的脾气", 18, ink));
            energyInput = AddSlider(settings, "活力", "沉稳", "活泼", 0, 100, out energyValue);
            affinityInput = AddSlider(settings, "亲近", "独立", "黏人", 0, 100, out affinityValue);
            spiceInput = AddSlider(settings, "傲娇", "直率", "嘴硬", 0, 100, out spiceValue);
            energyInput.ValueChanged += delegate { if (!refreshing) { profile.Energy = energyInput.Value; Changed(); } };
            affinityInput.ValueChanged += delegate { if (!refreshing) { profile.Affinity = affinityInput.Value; Changed(); } };
            spiceInput.ValueChanged += delegate { if (!refreshing) { profile.Spice = spiceInput.Value; Changed(); } };
            AddLabel(settings, "专注时说的一句话");
            mottoInput = new TextBox { MaxLength = 160, Padding = new Thickness(10, 7, 10, 7), TextWrapping = TextWrapping.Wrap, MinHeight = 58, AcceptsReturn = false };
            AutomationProperties.SetName(mottoInput, "角色口头禅");
            mottoInput.TextChanged += delegate { if (!refreshing) { profile.Motto = mottoInput.Text; Changed(); } };
            settings.Children.Add(mottoInput);
            sizeInput = AddSlider(settings, "桌宠尺寸", "小巧", "醒目", .6, 1.7, out sizeValue);
            sizeInput.TickFrequency = .1;
            sizeInput.IsSnapToTickEnabled = true;
            sizeInput.ValueChanged += delegate { if (!refreshing) { profile.Size = sizeInput.Value; Changed(); } };
            quietInput = new CheckBox { Content = "安静陪伴：关闭主动冒泡", Margin = new Thickness(0, 14, 0, 3) };
            quietInput.Checked += QuietChanged; quietInput.Unchecked += QuietChanged; settings.Children.Add(quietInput);
            settings.Children.Add(Text("点击和右键互动时，它仍会回应你。", 11, muted));
            ScrollViewer settingsScroll = new ScrollViewer { Content = settings, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            body.Children.Add(Card(settingsScroll, new Thickness(0, 0, 16, 0)));

            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(right, 1); body.Children.Add(right);
            StackPanel stage = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            previewName = Text("栗子", 24, ink); previewName.TextAlignment = TextAlignment.Center; stage.Children.Add(previewName);
            description = Text("", 12, muted); description.TextAlignment = TextAlignment.Center; description.Margin = new Thickness(0, 7, 0, 4); stage.Children.Add(description);
            preview.HorizontalAlignment = HorizontalAlignment.Center; stage.Children.Add(preview);
            reply = Text("点下方按钮，试试它的脾气。", 14, ink);
            reply.TextAlignment = TextAlignment.Center;
            stage.Children.Add(new Border { Background = ColorBrush("#F6F2FA"), CornerRadius = new CornerRadius(13), Padding = new Thickness(14), MinHeight = 70, Child = reply });
            WrapPanel reactions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 15, 0, 0) };
            reactions.Children.Add(Button("摸摸头", delegate { React("pet"); }, false));
            reactions.Children.Add(Button("打招呼", delegate { React("hello"); }, false));
            reactions.Children.Add(Button("专注鼓励", delegate { React("focus"); }, false));
            stage.Children.Add(reactions);
            right.Children.Add(Card(new ScrollViewer { Content = stage, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, new Thickness(0, 0, 0, 14)));

            StackPanel controls = new StackPanel();
            WrapPanel actions = new WrapPanel();
            actions.Children.Add(Button("保存角色", delegate { SaveProfile(true); }, true));
            visibilityButton = Button("隐藏桌宠", delegate { companion.SetCompanionVisible(!companion.IsVisible); }, false); actions.Children.Add(visibilityButton);
            actions.Children.Add(Button("收起工坊", delegate { companion.SetCompanionVisible(true); Hide(); }, false));
            controls.Children.Add(actions);
            controls.Children.Add(Text("桌宠：轻点摸头 · 拖动换位置 · 右键打开工坊\n关闭工坊会退出；收起后可从托盘恢复。", 11, muted));
            Border controlCard = Card(controls, new Thickness(0)); Grid.SetRow(controlCard, 1); right.Children.Add(controlCard);

            status = Text("", 11, muted); status.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(status, 2); root.Children.Add(status);
        }

        private void AddLabel(Panel parent, string value)
        {
            TextBlock label = Text(value, 12, muted); label.Margin = new Thickness(0, 14, 0, 7); parent.Children.Add(label);
        }

        private Slider AddSlider(Panel parent, string title, string low, string high, double minimum, double maximum, out TextBlock value)
        {
            Grid labels = new Grid { Margin = new Thickness(0, 15, 0, 5) };
            TextBlock label = Text(title + "  ·  " + low + " → " + high, 12, muted); labels.Children.Add(label);
            value = Text("", 12, accent); value.HorizontalAlignment = HorizontalAlignment.Right; labels.Children.Add(value); parent.Children.Add(labels);
            Slider slider = new Slider { Minimum = minimum, Maximum = maximum, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 0) };
            AutomationProperties.SetName(slider, title); parent.Children.Add(slider); return slider;
        }

        private void LoadFields()
        {
            bool previous = refreshing; refreshing = true;
            nameInput.Text = profile.Name; mottoInput.Text = profile.Motto; kindInput.SelectedIndex = (int)profile.Kind;
            energyInput.Value = profile.Energy; affinityInput.Value = profile.Affinity; spiceInput.Value = profile.Spice;
            sizeInput.Value = profile.Size; quietInput.IsChecked = profile.Quiet;
            refreshing = previous;
        }

        private void QuietChanged(object sender, RoutedEventArgs args)
        {
            if (refreshing) return;
            profile.Quiet = quietInput.IsChecked == true; Changed();
        }

        private void Changed()
        {
            dirty = true; RefreshPreview();
            status.Text = "修改已在桌面生效 · 点击“保存角色”保留设置";
        }

        private void RefreshPreview()
        {
            preview.Profile = profile;
            previewName.Text = String.IsNullOrWhiteSpace(profile.Name) ? "你的小伙伴" : profile.Name;
            description.Text = Personality.Describe(profile);
            energyValue.Text = profile.Energy.ToString("0", CultureInfo.InvariantCulture);
            affinityValue.Text = profile.Affinity.ToString("0", CultureInfo.InvariantCulture);
            spiceValue.Text = profile.Spice.ToString("0", CultureInfo.InvariantCulture);
            sizeValue.Text = profile.Size.ToString("0.0", CultureInfo.InvariantCulture) + "×";
            imageStatus.Text = String.IsNullOrEmpty(profile.ImagePath) ? "PNG / JPG · 推荐透明背景 PNG" : "图片已导入 · 以贴纸和轻动效展示";
            companion.ApplyProfile(profile);
        }

        private void React(string situation)
        {
            preview.Pose = situation;
            // Show exactly the line displayed by the real companion, without consuming a second reply.
            reply.Text = Personality.Respond(profile, situation);
            companion.ReactWithText(situation, reply.Text);
        }

        private void ImportImage()
        {
            OpenFileDialog picker = new OpenFileDialog { Title = "选择角色图片", Filter = "角色图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
            if (picker.ShowDialog(this) != true) return;
            try
            {
                profile.ImagePath = ProfileStore.ImportImage(picker.FileName); profile.Kind = PetKind.Image;
                LoadFields(); Changed(); status.Text = "图片已复制到本地 · 保存角色即可保留";
            }
            catch (Exception error)
            {
                if (error is OutOfMemoryException) throw;
                MessageBox.Show(this, "没有导入成功。请选择 10 MB 以内、宽高不超过 4096 像素的 PNG 或 JPG 图片。", "图片导入", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveProfile(bool announce)
        {
            try
            {
                profile.Normalize(); ProfileStore.Save(profile); LoadFields(); RefreshPreview(); dirty = false;
                if (announce) status.Text = "已保存到本地 · 下次打开还是这个小伙伴";
            }
            catch (Exception error)
            {
                if (error is OutOfMemoryException) throw;
                if (announce || dirty) MessageBox.Show(this, "暂时没有保存成功。请把整个文件夹放在可写的位置后再试。", "保存角色", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenStudio()
        {
            if (exiting) return;
            Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate();
        }

        private void ExitApplication()
        {
            if (exiting) return;
            exiting = true;
            SaveProfile(false);
            Application.Current.Shutdown();
        }

        private void CreateTray()
        {
            tray = new Forms.NotifyIcon { Text = "灵伴工坊 · 双击恢复工坊", Icon = System.Drawing.SystemIcons.Application, Visible = true };
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开角色工坊", null, delegate { OpenStudio(); });
            menu.Items.Add("显示 / 隐藏桌宠", null, delegate { companion.SetCompanionVisible(!companion.IsVisible); });
            menu.Items.Add("退出灵伴工坊", null, delegate { ExitApplication(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { OpenStudio(); };
        }
    }
}
