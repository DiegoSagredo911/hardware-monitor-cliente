using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using Newtonsoft.Json;
using System;
using hardwareMonitor;
namespace HardwareMonitor
{

    internal class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;

        static Config config = ConfigLoader.LoadConfig();

        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static NotifyIcon trayIcon;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Oculta la ventana de consola
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_HIDE);

                RunApp();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Asegúrate de que el archivo de configuración 'config.json' esté presente y sea válido.");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
            }
            finally
            {
                _cts?.Cancel();
                _cts?.Dispose();
                trayIcon?.Dispose();
            }
        }

        static void RunApp()
        {
            // Cargar configuración e icono
            trayIcon = new NotifyIcon();
            if (File.Exists(config.IconPath))
            {
                trayIcon.Icon = Icon.ExtractAssociatedIcon(config.IconPath);
            }
            else
            {
                trayIcon.Icon = SystemIcons.Application;
            }
            trayIcon.Text = config.Title ?? "Hardware Monitor";
            trayIcon.Visible = true;

            // Menú contextual para permitir cerrar la app
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Salir", null, OnExit);
            trayIcon.ContextMenuStrip = contextMenu;

            ClientConnection cliente = new ClientConnection(config.URL, config.Port, config.Delay);
            cliente.computer(config.ComputerName ?? Environment.MachineName, config.Group ?? "");

            // Ejecutar el envío de datos en segundo plano para no congelar el bucle de mensajes de Windows
            Task.Run(async () =>
            {
                await cliente.senddata(_cts.Token);
            }, _cts.Token);

            Application.Run(); // Mantiene el mensaje loop de Windows activo para el NotifyIcon
        }

        public static class ConfigLoader
        {
            public static Config LoadConfig(string path = "config.json")
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Archivo de configuración no encontrado: {path}");
                    Environment.Exit(1);
                }

                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Config>(json);
            }
        }

        static void OnExit(object sender, EventArgs e)
        {
            _cts.Cancel();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
            }
            Application.Exit();
        }
    }
}
