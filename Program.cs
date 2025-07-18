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

        static NotifyIcon trayIcon;

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
                // Asegúrate de liberar recursos si es necesario
                trayIcon?.Dispose();
            }
        }

        static void RunApp()
        {
            
            // Cargar configuración
            trayIcon = new NotifyIcon();
            trayIcon.Icon = Icon.ExtractAssociatedIcon(config.IconPath); // Puedes poner un ícono personalizado
            trayIcon.Text = config.Title;
            trayIcon.Visible = true;


            

            ClientConnection cliente = new ClientConnection(config.URL, config.Port, config.Delay);
            cliente.computer(config.ComputerName ?? Environment.MachineName, config.Group ?? "");

            cliente.senddata().GetAwaiter().GetResult();

            Application.Run(); // Para que el icono se mantenga
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
        //evento exit
        static void OnExit(object sender, EventArgs e)
        {


        }


    }

}
