using MQTTnet;
using MQTTnet.Client;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace hardwareMonitor
{
    public class ClientConnection
    {
        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private CancellationTokenSource _screenCts;
        private IMqttClient _activeMqttClient;
        private CancellationToken _globalToken;

        public string url { get; set; } = "localhost"; // URL por defecto para el servidor MQTT, se puede cambiar si es necesario

        public int puerto { get; set; } = 1883; // Puerto por defecto para MQTT

        public int delay = 5000; // Tiempo de espera entre envíos en milisegundos

        public string computerName { get; set; } = Environment.MachineName; // Nombre del equipo, se puede cambiar si es necesario

        public string group { get; set; } = ""; // Grupo de dispositivos para enviar datos

        public ClientConnection() { }

        public ClientConnection(string url, int puerto)
        {
            this.url = url;
            this.puerto = puerto;
        }

        public ClientConnection(string url, int puerto, int delay)
        {
            this.url = url;
            this.puerto = puerto;
            this.delay = delay;
        }

        public void computer(string computerName, string group = "")
        {
            if (computerName.Trim().Length != 0)
            {
                this.computerName = computerName.Trim();
            }

            if (group.Trim().Length != 0)
            {
                this.group = group.Trim();
            }
        }

        public async Task senddata(CancellationToken cancellationToken = default)
        {
            _globalToken = cancellationToken;
            var factory = new MqttFactory();
            using (var mqttClient = factory.CreateMqttClient())
            {
                _activeMqttClient = mqttClient;
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(url, puerto)
                    .Build();

                // Escuchar comandos remotos y eventos de entrada enviados vía MQTT
                mqttClient.ApplicationMessageReceivedAsync += async e =>
                {
                    try
                    {
                        var topic = e.ApplicationMessage.Topic;
                        var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

                        // Si es evento de entrada de mouse/teclado para control remoto
                        if (topic == $"hardware/input/{computerName}")
                        {
                            using (var doc = JsonDocument.Parse(payload))
                            {
                                HandleRemoteInput(doc.RootElement);
                            }
                            await Task.CompletedTask;
                            return;
                        }

                        Console.WriteLine($"[MQTT Command] Tópico: {topic} | Payload: {payload}");

                        using (var doc = JsonDocument.Parse(payload))
                        {
                            var root = doc.RootElement;
                            string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() : null;
                            JsonElement paramsEl = root.TryGetProperty("params", out var parProp) ? parProp : default;

                            ExecuteCommand(action, paramsEl);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al procesar mensaje MQTT: {ex.Message}");
                    }
                    await Task.CompletedTask;
                };

                var computer = new Computer
                {
                    CPUEnabled = true,
                    GPUEnabled = true,
                    RAMEnabled = true,
                    MainboardEnabled = true,
                    FanControllerEnabled = true,
                    HDDEnabled = true
                };

                try
                {
                    computer.Open();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (!mqttClient.IsConnected)
                            {
                                Console.WriteLine($"Intentando conectar a MQTT broker en {url}:{puerto}...");
                                await mqttClient.ConnectAsync(options, cancellationToken);
                                Console.WriteLine("Conectado exitosamente al broker MQTT.");

                                // Suscribirse a tópicos de comandos: individual, por sala/grupo y general
                                await mqttClient.SubscribeAsync($"hardware/commands/pc/{computerName}", cancellationToken: cancellationToken);
                                if (!string.IsNullOrWhiteSpace(group))
                                {
                                    await mqttClient.SubscribeAsync($"hardware/commands/group/{group}", cancellationToken: cancellationToken);
                                }
                                await mqttClient.SubscribeAsync("hardware/commands/all", cancellationToken: cancellationToken);

                                // Suscribirse al canal de eventos de entrada interactiva (mouse/teclado)
                                await mqttClient.SubscribeAsync($"hardware/input/{computerName}", cancellationToken: cancellationToken);

                                Console.WriteLine($"Suscrito a comandos y control para PC '{computerName}'.");
                            }

                            var structuredData = new Dictionary<string, object>
                            {
                                ["timestamp"] = DateTime.UtcNow.ToString("o"), // formato ISO 8601
                                ["unixtimestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                ["computerName"] = computerName,
                                ["group"] = group ?? null, // si no se especifica grupo, se usa "default"
                                ["hardware"] = new Dictionary<string, object>()
                            };

                            var hardwareDict = (Dictionary<string, object>)structuredData["hardware"];

                            foreach (var hardware in computer.Hardware)
                            {
                                hardware.Update();
                                foreach (var subhardware in hardware.SubHardware)
                                {
                                    subhardware.Update();
                                }

                                string hardwareType = hardware.HardwareType.ToString();
                                string hardwareName = hardware.Name;

                                if (!hardwareDict.ContainsKey(hardwareType))
                                    hardwareDict[hardwareType] = new Dictionary<string, object>();

                                var typeGroup = (Dictionary<string, object>)hardwareDict[hardwareType];

                                if (!typeGroup.ContainsKey(hardwareName))
                                    typeGroup[hardwareName] = new Dictionary<string, object>();

                                var nameGroup = (Dictionary<string, object>)typeGroup[hardwareName];

                                foreach (var sensor in hardware.Sensors)
                                {
                                    if (sensor.Value != null)
                                    {
                                        string sensorType = sensor.SensorType.ToString();
                                        string sensorName = sensor.Name;

                                        if (!nameGroup.ContainsKey(sensorType))
                                            nameGroup[sensorType] = new Dictionary<string, object>();

                                        var sensorGroup = (Dictionary<string, object>)nameGroup[sensorType];
                                        sensorGroup[sensorName] = sensor.Value;
                                    }
                                }
                            }

                            var json = JsonSerializer.Serialize(structuredData, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });

                            var message = new MqttApplicationMessageBuilder()
                                .WithTopic("hardware/status")
                                .WithPayload(json)
                                .Build();

                            await mqttClient.PublishAsync(message, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error de conexión/envío MQTT: {ex.Message}");
                        }

                        try
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    StopScreenCapture();

                    try
                    {
                        computer.Close();
                    }
                    catch { }

                    if (mqttClient.IsConnected)
                    {
                        try
                        {
                            await mqttClient.DisconnectAsync();
                        }
                        catch { }
                    }
                }
            }
        }

        private void ExecuteCommand(string action, JsonElement parameters)
        {
            if (string.IsNullOrWhiteSpace(action)) return;

            action = action.Trim().ToLowerInvariant();

            int delaySeconds = 60;
            string text = "";

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("delaySeconds", out var delayProp) && delayProp.TryGetInt32(out var parsedDelay))
                {
                    delaySeconds = Math.Max(0, parsedDelay);
                }
                if (parameters.TryGetProperty("text", out var textProp))
                {
                    text = textProp.GetString() ?? "";
                }
            }

            switch (action)
            {
                case "start_screen":
                    StartScreenCapture(parameters);
                    break;

                case "stop_screen":
                    StopScreenCapture();
                    break;

                case "input":
                    HandleRemoteInput(parameters);
                    break;

                case "shutdown":
                    string shutdownMsg = string.IsNullOrWhiteSpace(text)
                        ? "El administrador ha iniciado el apagado del equipo."
                        : text.Replace("\"", "'");
                    Console.WriteLine($"[Comando] Apagado solicitado en {delaySeconds}s. Mensaje: {shutdownMsg}");
                    try
                    {
                        Process.Start("shutdown.exe", $"/s /t {delaySeconds} /c \"{shutdownMsg}\"");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al ejecutar shutdown: {ex.Message}");
                    }
                    break;

                case "restart":
                    string restartMsg = string.IsNullOrWhiteSpace(text)
                        ? "El administrador ha iniciado el reinicio del equipo."
                        : text.Replace("\"", "'");
                    Console.WriteLine($"[Comando] Reinicio solicitado en {delaySeconds}s. Mensaje: {restartMsg}");
                    try
                    {
                        Process.Start("shutdown.exe", $"/r /t {delaySeconds} /c \"{restartMsg}\"");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al ejecutar restart: {ex.Message}");
                    }
                    break;

                case "abort_shutdown":
                    Console.WriteLine("[Comando] Cancelación de apagado programado solicitada.");
                    try
                    {
                        Process.Start("shutdown.exe", "/a");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al cancelar apagado: {ex.Message}");
                    }
                    break;

                case "lock":
                    Console.WriteLine("[Comando] Bloqueo de estación de trabajo.");
                    try
                    {
                        LockWorkStation();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al bloquear estación: {ex.Message}");
                    }
                    break;

                case "message":
                    string userMsg = string.IsNullOrWhiteSpace(text)
                        ? "Aviso del Administrador del Laboratorio"
                        : text;
                    Console.WriteLine($"[Comando] Mensaje emergente: {userMsg}");
                    Task.Run(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                userMsg,
                                "Aviso del Administrador",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al mostrar MessageBox: {ex.Message}");
                        }
                    });
                    break;

                default:
                    Console.WriteLine($"[Comando] Acción no reconocida: {action}");
                    break;
            }
        }

        private void StartScreenCapture(JsonElement parameters)
        {
            StopScreenCapture();

            if (_activeMqttClient == null || !_activeMqttClient.IsConnected)
            {
                Console.WriteLine("[Screen Stream] No se puede iniciar captura: cliente MQTT no conectado.");
                return;
            }

            _screenCts = CancellationTokenSource.CreateLinkedTokenSource(_globalToken);
            var token = _screenCts.Token;

            int fps = 5;
            long quality = 50L;
            int targetWidth = 1280;

            if (parameters.ValueKind == JsonValueKind.Object)
            {
                if (parameters.TryGetProperty("fps", out var fpsProp) && fpsProp.TryGetInt32(out var parsedFps))
                {
                    fps = Math.Max(1, Math.Min(12, parsedFps));
                }
                if (parameters.TryGetProperty("quality", out var qProp) && qProp.TryGetInt64(out var parsedQ))
                {
                    quality = Math.Max(25L, Math.Min(85L, parsedQ));
                }
            }

            int intervalMs = 1000 / fps;

            Task.Run(async () =>
            {
                Console.WriteLine($"[Screen Stream] Sesión iniciada para '{computerName}' ({fps} FPS, calidad {quality}%).");
                var topic = $"hardware/screen/{computerName}";

                ImageCodecInfo jpegCodec = null;
                foreach (var codec in ImageCodecInfo.GetImageEncoders())
                {
                    if (codec.FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpegCodec = codec;
                        break;
                    }
                }

                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                // Límite de seguridad: máximo 5 minutos continuos de transmisión sin renovar
                var sessionTimeout = DateTime.UtcNow.AddMinutes(5);

                try
                {
                    while (!token.IsCancellationRequested && DateTime.UtcNow < sessionTimeout)
                    {
                        if (!_activeMqttClient.IsConnected)
                        {
                            await Task.Delay(500, token);
                            continue;
                        }

                        var bounds = Screen.PrimaryScreen.Bounds;
                        using (var screenBmp = new Bitmap(bounds.Width, bounds.Height))
                        {
                            using (var g = Graphics.FromImage(screenBmp))
                            {
                                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                            }

                            int destWidth = bounds.Width;
                            int destHeight = bounds.Height;
                            if (destWidth > targetWidth)
                            {
                                destHeight = (int)((float)destHeight * targetWidth / destWidth);
                                destWidth = targetWidth;
                            }

                            using (var resizedBmp = new Bitmap(destWidth, destHeight))
                            {
                                using (var rg = Graphics.FromImage(resizedBmp))
                                {
                                    rg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                                    rg.DrawImage(screenBmp, 0, 0, destWidth, destHeight);
                                }

                                using (var ms = new MemoryStream())
                                {
                                    if (jpegCodec != null)
                                    {
                                        resizedBmp.Save(ms, jpegCodec, encoderParams);
                                    }
                                    else
                                    {
                                        resizedBmp.Save(ms, ImageFormat.Jpeg);
                                    }

                                    string base64 = Convert.ToBase64String(ms.ToArray());
                                    var frameJson = JsonSerializer.Serialize(new
                                    {
                                        computerName = computerName,
                                        timestamp = DateTime.UtcNow.ToString("o"),
                                        frame = "data:image/jpeg;base64," + base64
                                    });

                                    var msg = new MqttApplicationMessageBuilder()
                                        .WithTopic(topic)
                                        .WithPayload(frameJson)
                                        .Build();

                                    await _activeMqttClient.PublishAsync(msg, token);
                                }
                            }
                        }

                        await Task.Delay(intervalMs, token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Screen Stream] Finalización de transmisión: {ex.Message}");
                }
                finally
                {
                    encoderParams.Dispose();
                    Console.WriteLine($"[Screen Stream] Sesión de pantalla finalizada para '{computerName}'. Memoria liberada.");
                }
            }, token);
        }

        private void StopScreenCapture()
        {
            if (_screenCts != null)
            {
                try
                {
                    _screenCts.Cancel();
                    _screenCts.Dispose();
                }
                catch { }
                _screenCts = null;
            }
        }

        private void HandleRemoteInput(JsonElement parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object) return;

            var bounds = Screen.PrimaryScreen.Bounds;

            double xFrac = 0;
            double yFrac = 0;

            if (parameters.TryGetProperty("x", out var xProp))
            {
                xFrac = xProp.GetDouble();
                if (xFrac > 1.0) xFrac /= 100.0;
            }

            if (parameters.TryGetProperty("y", out var yProp))
            {
                yFrac = yProp.GetDouble();
                if (yFrac > 1.0) yFrac /= 100.0;
            }

            int screenX = (int)Math.Round(Math.Max(0, Math.Min(1.0, xFrac)) * (bounds.Width - 1));
            int screenY = (int)Math.Round(Math.Max(0, Math.Min(1.0, yFrac)) * (bounds.Height - 1));

            string type = parameters.TryGetProperty("type", out var tProp) ? tProp.GetString() : "click";
            string button = parameters.TryGetProperty("button", out var bProp) ? bProp.GetString() : "left";

            // Posicionar cursor
            SetCursorPos(screenX, screenY);

            if (type == "click")
            {
                if (button == "right")
                {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)screenX, (uint)screenY, 0, 0);
                    mouse_event(MOUSEEVENTF_RIGHTUP, (uint)screenX, (uint)screenY, 0, 0);
                }
                else
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)screenX, (uint)screenY, 0, 0);
                    mouse_event(MOUSEEVENTF_LEFTUP, (uint)screenX, (uint)screenY, 0, 0);
                }
            }
            else if (type == "mousedown")
            {
                uint flag = button == "right" ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
                mouse_event(flag, (uint)screenX, (uint)screenY, 0, 0);
            }
            else if (type == "mouseup")
            {
                uint flag = button == "right" ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;
                mouse_event(flag, (uint)screenX, (uint)screenY, 0, 0);
            }
        }
    }
}

