## 🖥️ Hardware Monitor Cliente

Aplicación de escritorio en C# que recopila métricas de hardware de un equipo y las publica en tiempo real a través de MQTT a un servidor central. Está pensada para ejecutarse en los dispositivos que deseas monitorear.

---

### ⚙️ Tecnologías Usadas

- `C# / .NET Framework`
- [`MQTTnet`](https://github.com/dotnet/MQTTnet) — Cliente MQTT moderno para .NET
- [`OpenHardwareMonitorLib`](https://github.com/openhardwaremonitor/openhardwaremonitor) — Librería (usada como `.dll`) para acceder a sensores de hardware
- `JSON` para serialización de datos
- `MQTT` para comunicación con el backend

---

### 📦 Estructura del Proyecto

```

hardwareMonitor/
│   App.config
│   ClientConnection.cs      # Conexión MQTT y envío de datos
│   Config.cs                # Carga de configuración desde config.json
│   Program.cs               # Punto de entrada
│   config.json              # Configuración por defecto
│   ico.ico                  # Icono del programa
│   hardwareMonitor.sln      # Solución de Visual Studio
│   packages.config
│   README.md
│
├───bin/
│   └───Debug/
│       ├── Hardware Monitor.exe       # Ejecutable principal
│       ├── config.json                # Configuración en tiempo de ejecución
│       ├── OpenHardwareMonitorLib.dll
│       └── (otras dependencias .dll)
│
├───openhardwaremonitor/
│   ├── OpenHardwareMonitorLib.dll     # Librería externa usada como referencia
│   └── License/License.html           # Licencia MPL-2.0 original

```

---

### 🚀 Ejecución

1. Clona el repositorio:

```bash
git clone https://github.com/DiegoSagredo911/hardware-monitor-cliente
```

2. Abre `hardwareMonitor.sln` en Visual Studio.

3. Asegúrate de tener las dependencias necesarias (`MQTTnet`, `OpenHardwareMonitorLib.dll`, etc.).

4. Compila el proyecto (`F6`) y dirígete a `bin/Debug`.

5. Ejecuta el archivo `Hardware Monitor.exe`.

---

### 🧾 Configuración desde `config.json`

Dentro de la carpeta `bin/Debug`, encontrarás el archivo `config.json`, donde puedes modificar los parámetros de configuración sin recompilar.

#### Ejemplo de `config.json`:

```json
{
  "Title": "HardwareMonitor - Diego Sagredo",
  "URL": "localhost",
  "Port": "1883",
  "IconPath": "ico.ico",
  "Delay": "5000"
  // "ComputerName": "Lab_500",
  // "Group": "HardwareMonitor"
}
```

#### Campos configurables:

- `Title`: Título de la aplicación (informativo).
- `URL`: Dirección del broker MQTT.
- `Port`: Puerto del broker (1883 por defecto).
- `IconPath`: Ruta al ícono de la aplicación.
- `Delay`: Tiempo entre envíos (en milisegundos).
- `ComputerName`: _(opcional)_ Nombre personalizado del equipo.
- `Group`: _(opcional)_ Grupo o ubicación del equipo.

---

### 📡 Funcionamiento

La clase `ClientConnection` establece una conexión MQTT usando `MQTTnet`, obtiene métricas de hardware usando `OpenHardwareMonitorLib.dll`, estructura los datos jerárquicamente y los envía como JSON al tópico `hardware/status` cada _X_ milisegundos.

#### Ejemplo de JSON enviado:

```json
{
  "timestamp": "2025-07-18T18:26:40.123Z",
  "unixtimestamp": 1752847600,
  "computerName": "PC-001",
  "group": "oficina",
  "hardware": {
    "CPU": {
      "AMD Ryzen 5 3600": {
        "Temperature": {
          "Core (Tctl/Tdie)": 55.2
        }
      }
    }
  }
}
```

---

### 🔗 Proyectos Relacionados

- 🧠 [hardware-monitor-back-end](https://github.com/DiegoSagredo911/hardware-monitor-back-end) — API y almacenamiento
- 👁️ [hardware-monitor-front-end](https://github.com/DiegoSagredo911/hardware-monitor-front-end) — Panel de administración
- 💻 Este repositorio — Cliente de monitoreo para PCs

---

### 📜 Licencias

#### ✅ Código Propio

El código fuente escrito para este proyecto está bajo la [Licencia MIT](https://opensource.org/licenses/MIT).

#### 📦 Librerías de Terceros

- [`OpenHardwareMonitorLib`](https://github.com/openhardwaremonitor/openhardwaremonitor)
  Distribuida bajo la [Mozilla Public License 2.0 (MPL-2.0)](https://www.mozilla.org/MPL/2.0/).
  La licencia completa se encuentra en [`/openhardwaremonitor/License/License.html`](./openhardwaremonitor/License/License.html).

- [`MQTTnet`](https://github.com/dotnet/MQTTnet)
  Distribuido bajo Licencia MIT.

---

### 🤝 Contribuciones

¡Las contribuciones son bienvenidas!

Puedes abrir Issues o Pull Requests para:

- Reportar errores
- Proponer mejoras
- Sugerir compatibilidad con nuevos sensores
