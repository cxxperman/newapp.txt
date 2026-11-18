// newapp.txt. MainWindow.cs — окно: шапка со значком и названием, один стеклянный блок, строка состояния.
// Всё собрано из общих элементов (Ui.cs) и токенов (Tokens.cs): своих цветов, радиусов и длительностей здесь нет.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevTxt.NewApp
{
    class MainWindow : Window
    {
        const double W = 560, H = 340;
        // Значок-заготовка. Нарисуйте свой: строки из «.» (пусто), «#» (тело) и «E» (глаз) — см. README, «Значок».
        internal static readonly string[] Sprite = { "..######..", ".########.", "##########", "##E####E##", "##E####E##", "##########", "##########", ".########.", "..######.." };

        readonly bool demo;
        readonly Switch onTop;
        readonly Status status = new Status();
        readonly Btn coreBtn = new Btn();

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainWindow(bool demo)
        {
            this.demo = demo;
            Title = Program.Id + ".txt";
            Width = W; Height = H;
            // Родное окно Windows 11: системная рамка и тень, Mica, тема Windows, прилипание к краям экрана
            Ui.ThemeFollows = !demo;
            Ui.Native(this, false);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Bg;
            FontFamily = Ui.TextFont;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            if (demo) Prefs.OnTop = true;   // выдуманное состояние для снимков
            onTop = new Switch(Prefs.OnTop, delegate(bool v) { Prefs.SetOnTop(v); });
            Topmost = Prefs.OnTop && !demo;
            Content = Build();
            Render();

            // Настройку могут поменять и здесь, и из core.txt — окно в любом случае показывает то, что есть
            Prefs.Changed += delegate { onTop.Value = Prefs.OnTop; Topmost = Prefs.OnTop; };
            AppHub.StatusChanged += Render;

        }

        // ───────── Разметка ─────────
        FrameworkElement Build()
        {
            var root = new Grid { Background = Ui.Bg, Width = W, Height = H, ClipToBounds = true };
            root.Children.Add(Ui.Backdrop());
            var layout = new DockPanel { LastChildFill = true };
            root.Children.Add(layout);

            // Шапка: за неё окно перетаскивается
            var header = new Grid { Height = 92, Background = Brushes.Transparent };
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) try { DragMove(); } catch { } };
            var mark = Ui.Sprite(Sprite, 40);
            mark.VerticalAlignment = VerticalAlignment.Center;
            var title = Ui.Brand(Program.Id, Tokens.Size.Display, FontWeights.Bold);
            title.Margin = new Thickness(10, 0, 0, 2); title.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(32, 22, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { mark, title } });

            header.Children.Add(Ui.CaptionButtons(this, Close));   // кнопки окна как у Windows 11
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header);

            // Низ: версия и подпись
            var footer = Ui.T("версия " + Program.VersionText + " · dev.txt™", Tokens.Size.Caption, Ui.Label3, FontWeights.Normal);
            footer.Margin = new Thickness(32, 0, 32, 24);
            DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(footer);

            // Блок: настройка с переключателем, под ней — связь с core.txt и кнопка
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            text.Children.Add(Ui.T("Поверх остальных окон", Tokens.Size.Body, Ui.Label, FontWeights.Normal));
            var hint = Ui.T("Окно приложения не прячется за другими. Та же настройка есть в core.txt", Tokens.Size.Micro, Ui.Label3, FontWeights.Normal);
            hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 1, 0, 0);
            text.Children.Add(hint);
            var setting = new DockPanel { LastChildFill = true, MinHeight = 30 };
            onTop.Root.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(onTop.Root, Dock.Right);
            setting.Children.Add(onTop.Root);
            setting.Children.Add(text);

            var line = new Border { Height = 1, Background = Ui.Solid(Tokens.Dark.Separator), Margin = new Thickness(0, 16, 0, 16) };

            var link = new DockPanel { LastChildFill = true };
            coreBtn.Root.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(coreBtn.Root, Dock.Right);
            link.Children.Add(coreBtn.Root);
            status.Root.VerticalAlignment = VerticalAlignment.Center;
            status.Root.Margin = new Thickness(0, 0, 16, 0);
            link.Children.Add(status.Root);

            var block = Ui.Block(new StackPanel { Children = { setting, line, link } }, true);
            block.Margin = new Thickness(32, 0, 32, 0);
            block.VerticalAlignment = VerticalAlignment.Top;
            layout.Children.Add(block);
            return root;
        }

        FrameworkElement WindowButton(string glyph, string tip, bool danger, Action click)
        {
            var t = new TextBlock { Text = glyph, FontFamily = Ui.IconFont, FontSize = 10, Foreground = Ui.Label2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Width = 36, Height = 26, CornerRadius = new CornerRadius(Tokens.Radius.Tip), Background = Brushes.Transparent, Child = t, ToolTip = tip };
            b.MouseEnter += delegate { b.Background = danger ? Ui.Solid(Tokens.Palette.Red) : Ui.Fill2; t.Foreground = Ui.Label; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; t.Foreground = Ui.Label2; };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        // Приводит строку связи в соответствие с тем, что есть
        void Render()
        {
            bool online = demo || AppHub.Online;
            if (online) status.Set("Связь с core.txt есть", Status.Kind.Ok);
            else if (CoreApp.Installed) status.Set("core.txt не запущен — всё работает и без него", Status.Kind.None);
            else status.Set(CoreApp.Pitch, Status.Kind.None);
            // Дорога к core.txt: открыть, а если его нет — скачать (правило dev.txt: она есть в каждом приложении)
            coreBtn.Set(CoreApp.Label(online), online || CoreApp.Installed ? (Color?)null : Tokens.Palette.Brand, true, delegate { if (!demo) CoreApp.Go(AppHub.Online, AppHub.ShowCore); });
        }

        public void ShowWindow()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        // Рисует окно в файл PNG, не показывая его на экране
        public void Shot(string file)
        {
            var root = (FrameworkElement)Content;
            Content = null;
            root.Measure(new Size(W, H));
            root.Arrange(new Rect(0, 0, W, H));
            root.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)(W * 1.5), (int)(H * 1.5), 144, 144, PixelFormats.Pbgra32);
            bmp.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var f = System.IO.File.Create(file)) enc.Save(f);
        }
    }
}
