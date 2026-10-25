using System;
using System.IO;
using System.Windows;

namespace Lingban
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test")
            {
                try
                {
                    string report = SelfTests.Run();
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt"), report, System.Text.Encoding.UTF8);
                    Console.WriteLine(report);
                    return 0;
                }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt"), "FAILED: " + error, System.Text.Encoding.UTF8);
                    Console.Error.WriteLine(error);
                    return 1;
                }
            }
            Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                MessageBox.Show("这次操作没有完成。请重新打开灵伴工坊再试一次。", "灵伴工坊", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
                app.Shutdown(1);
            };
            StudioWindow studio = new StudioWindow(ProfileStore.Load());
            app.MainWindow = studio;
            app.Run(studio);
            return 0;
        }
    }
}
