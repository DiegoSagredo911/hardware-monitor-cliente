using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hardwareMonitor
{
    public class Config
    {
        public string Title { get; set; } // Título de la aplicación para la bandeja del sistema
        public string URL { get; set; } // URL del servidor MQTT
        public int Port { get; set; } // Puerto del servidor MQTT
        public string IconPath { get; set; } // Ruta del ícono para la bandeja del sistema
        public int Delay { get; set; } // Tiempo de espera entre envíos en milisegundos
        public string ComputerName { get; set; } // Nombre del equipo, se puede cambiar si es necesario
        public string Group { get; set; } // Grupo de dispositivos para enviar datos
    }

}
