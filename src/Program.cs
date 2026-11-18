// newapp.txt — заготовка приложения dev.txt™: окно в общем виде и связь с хабом с первого дня.
// Ключи для проверки:
//   --demo              выдуманное состояние; хаб, диск и сеть не трогаются
//   --shot файл.png     нарисовать окно в файл и выйти (обычно вместе с --demo)
//   --selftest          проверить настройки, их описание для хаба и работу без хаба; код выхода 0 — всё в порядке
//   --headless          запуститься без окна: только связь с хабом (для проверок протокола)
// Для проверок же: NEWAPP_HUB_PORT — хаб на другом порту, NEWAPP_DATA — другая папка для настроек.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;

namespace DevTxt.NewApp
{
    static class Program
    {
        // Имя в хабе, в каталоге core.txt и в папке настроек — строчными латинскими; Name — имя файла и процесса
        public const string Id = "newapp", Name = "NewApp";

        public static string VersionText
        {
            get { Version v = typeof(Program).Assembly.GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        [STAThread]
        static int Main(string[] args)
        {
            bool demo = Array.IndexOf(args, "--demo") >= 0, headless = Array.IndexOf(args, "--headless") >= 0;
            string shot = null;
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--shot") shot = args[i + 1];
            if (Array.IndexOf(args, "--selftest") >= 0) return SelfTest.Run();

            bool created = true;
            Mutex mutex = null;
            if (shot == null && !demo) mutex = new Mutex(true, "devtxt_" + Id + "_single_instance", out created);
            if (!created)
            {
                // Уже запущено: просим показать окно и выходим
                try { EventWaitHandle.OpenExisting("devtxt_" + Id + "_show").Set(); } catch { }
                return 0;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (demo) Prefs.Persist = false; else Prefs.Load();
            var w = new MainWindow(demo);
            if (shot != null) { w.Shot(shot); return 0; }

            if (!demo)
            {
                AppHub.Show = w.ShowWindow;
                AppHub.Start(w.Dispatcher);
                var show = new EventWaitHandle(false, EventResetMode.AutoReset, "devtxt_" + Id + "_show");
                new Thread(delegate() { while (true) { show.WaitOne(); w.Dispatcher.BeginInvoke(new Action(w.ShowWindow)); } }) { IsBackground = true }.Start();
            }
            w.Closed += delegate { AppHub.Stop(); app.Shutdown(); };
            if (!headless) w.Show();
            app.Run();
            GC.KeepAlive(mutex);
            return 0;
        }
    }

    // Настройки приложения. Здесь одна — для примера; свои добавляются рядом по тому же образцу.
    static class Prefs
    {
        public static bool OnTop;            // окно поверх остальных
        public static bool Persist = true;   // false — ничего не писать на диск (--demo)
        public static event Action Changed;  // вызывается в потоке окна

        public static string Dir
        {
            get
            {
                string test = Environment.GetEnvironmentVariable("NEWAPP_DATA");
                if (!string.IsNullOrEmpty(test)) return test;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dev.txt", Program.Id);
            }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }

        public static void Load()
        {
            try
            {
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath)) as Dictionary<string, object>;
                object v;
                if (d != null && d.TryGetValue("onTop", out v) && v is bool) OnTop = (bool)v;
            }
            catch { }   // файла ещё нет или он испорчен — остаются значения по умолчанию
        }

        public static void Save()
        {
            if (!Persist) return;
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(new Dictionary<string, object> { { "onTop", OnTop } }));
            }
            catch { }
        }

        // true — значение действительно поменялось
        public static bool SetOnTop(bool on)
        {
            if (OnTop == on) return false;
            OnTop = on;
            Save();
            if (Changed != null) Changed();
            return true;
        }
    }

    static class SelfTest
    {
        static int failed;
        static void Check(bool ok, string what) { if (!ok) { failed++; Console.Error.WriteLine("FAIL  " + what); } }

        static bool Throws(Action a) { try { a(); return false; } catch { return true; } }

        static Dictionary<string, object> FirstItem(Dictionary<string, object> settings)
        {
            var groups = settings["groups"] as object[];
            var items = ((Dictionary<string, object>)groups[0])["items"] as object[];
            return (Dictionary<string, object>)items[0];
        }

        public static int Run()
        {
            Prefs.Persist = false;
            Prefs.OnTop = false;

            Check(System.Text.RegularExpressions.Regex.IsMatch(Program.Id, "^[a-z][a-z0-9-]{0,31}$") && Program.Id != "hub", "app id fits the hub rule");
            foreach (string row in MainWindow.Sprite) Check(row.Length == MainWindow.Sprite[0].Length, "sprite rows are of equal length");

            // Описание настроек — в том виде, в каком его ждёт core.txt
            var item = FirstItem(AppHub.Settings());
            Check((string)item["key"] == "onTop" && (string)item["type"] == "switch" && item["value"] is bool && !(bool)item["value"], "settings describe the switch, off by default");
            Check(!string.IsNullOrEmpty(item["label"] as string), "the switch has a label");
            string json = new JavaScriptSerializer().Serialize(HubLink.Msg("type", "state", "topic", AppHub.Topic, "data", AppHub.Settings()));
            var back = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            Check(back != null && (string)back["topic"] == Program.Id + ".settings" && FirstItem((Dictionary<string, object>)back["data"])["value"] is bool, "settings survive JSON");

            // settings.set: значение меняется, о нём сообщают один раз, чужой ключ и не то значение отклоняются
            int fired = 0;
            Action count = delegate { fired++; };
            Prefs.Changed += count;
            AppHub.Apply("onTop", true);
            Check(Prefs.OnTop && (bool)FirstItem(AppHub.Settings())["value"], "settings.set changes the value");
            AppHub.Apply("onTop", true);
            Check(fired == 1, "an unchanged value is not announced again, announced " + fired);
            Check(Throws(delegate { AppHub.Apply("noSuchKey", true); }), "unknown key is refused");
            Check(Throws(delegate { AppHub.Apply("onTop", "yes"); }), "a value of the wrong kind is refused");
            Check(Throws(delegate { AppHub.Apply(null, null); }), "empty arguments are refused");
            Check(Prefs.OnTop, "refused commands change nothing");
            Prefs.Changed -= count;

            // hub.theme: «Меньше движения» читается только из настоящего «да»
            Check(AppHub.ReadReduced(HubLink.Msg("mode", "dark", "accent", null, "reducedMotion", true)), "reducedMotion: true is read");
            Check(!AppHub.ReadReduced(HubLink.Msg("mode", "dark", "reducedMotion", false)) && !AppHub.ReadReduced(HubLink.Msg("reducedMotion", "yes")) && !AppHub.ReadReduced(null) && !AppHub.ReadReduced("x"), "anything else is not reduced motion");

            // Настройки переживают запись и чтение
            string dir = Path.Combine(Path.GetTempPath(), Program.Id + "-test-" + System.Diagnostics.Process.GetCurrentProcess().Id);
            Environment.SetEnvironmentVariable("NEWAPP_DATA", dir);
            try
            {
                Prefs.Persist = true;
                Prefs.OnTop = true;
                Prefs.Save();
                Prefs.OnTop = false;
                Prefs.Load();
                Check(Prefs.OnTop, "settings survive saving and loading");
                File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not json");
                Prefs.OnTop = false;
                Prefs.Load();
                Check(!Prefs.OnTop, "a broken settings file is ignored");
            }
            finally
            {
                Prefs.Persist = false;
                try { Directory.Delete(dir, true); } catch { }
            }

            // Без хаба: клиент ничего не бросает и остаётся «не на связи». Порт 9 никто не слушает.
            var link = new HubLink(Program.Id, Program.Name, Program.VersionText, 9);
            Check(!Throws(delegate { link.Start(); link.State(AppHub.Topic, AppHub.Settings()); link.Command("core", "show", null); Thread.Sleep(300); link.Stop(); }), "no hub — nothing throws");
            Check(!link.Online, "no hub — the app is simply offline");

            Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
