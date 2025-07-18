using MQTTnet;
using MQTTnet.Client;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;




namespace hardwareMonitor
{
    public class ClientConnection
    {
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

        public async Task senddata()
        {
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
            .WithTcpServer(url, puerto)
            .Build();

            await mqttClient.ConnectAsync(options, CancellationToken.None);

            var computer = new Computer
            {
                CPUEnabled = true,
                GPUEnabled = true,
                RAMEnabled = true,
                MainboardEnabled = true,
                FanControllerEnabled = true,
                HDDEnabled = true
            };
            computer.Open();

            while (true)
            {
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

                await mqttClient.PublishAsync(message, CancellationToken.None);

                await Task.Delay(delay);
            }
        }
    }
}

