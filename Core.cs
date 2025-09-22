using MelonLoader;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections;
using System.Text;
using UnityEngine;

[assembly: MelonInfo(typeof(Vooshi_TTS.Core), "Vooshi-TTS", "1.0.0", "Shr", null)]
[assembly: MelonGame("Shinymoon", "MateEngineX")]

namespace Vooshi_TTS { 

    public class TTSMessage
    {
        public string text { get; set; }
        public string tts { get; set; } // base64 audio
    }

    public class Core : MelonMod
    {
        private IConnection connection;
        private IModel channel;
        private string queueName = "tts-messages-mika";
        private CancellationTokenSource cts = new CancellationTokenSource();
        private AudioSource audioSource;



        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");

            var tts = new GameObject("TTS_AudioSource");
            UnityEngine.Object.DontDestroyOnLoad(tts);
            audioSource = tts.AddComponent<AudioSource>();
        }

        [Obsolete]
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Connecting to RabbitMQ...");
            Task.Run(() =>
            {
                try
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = "*",
                        Port = 5672,
                        UserName = "*",
                        Password = "*",
                        VirtualHost = "/",
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                        RequestedHeartbeat = 60
                    };

                    // Create connection and channel using RabbitMQ.Client 5.2.0 methods
                    connection = factory.CreateConnection();
                    channel = connection.CreateModel();

                    // Enable publisher confirms for reliability (optional)
                    channel.ConfirmSelect();

                    // Declare queue
                    channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null
                    );

                    // Set up consumer
                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (sender, ea) =>
                    {
                        try
                        {
                            var body = ea.Body;
                            var message = Encoding.UTF8.GetString(body);

                            // Process message asynchronously to avoid blocking the consumer thread

                            ProcessMessage(message);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Error handling received message: {ex.Message}");
                        }
                    };

                    consumer.Shutdown += (sender, ea) =>
                    {
                        MelonLogger.Msg($"[RabbitMQ] Consumer shutdown: {ea.ReplyText}");
                    };

                    // Start consuming messages
                    string consumerTag = channel.BasicConsume(
                        queue: queueName,
                        autoAck: true,
                        consumer: consumer
                    );

                    MelonLogger.Msg($"Successfully subscribed to RabbitMQ queue: {queueName}");
                    MelonLogger.Msg($"Consumer tag: {consumerTag}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to connect to RabbitMQ: {ex.Message}");
                    MelonLogger.Error($"Stack trace: {ex.StackTrace}");

                    // Attempt to reconnect after delay
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000, cts.Token);
                        if (!cts.Token.IsCancellationRequested)
                        {
                            MelonLogger.Msg("Attempting to reconnect to RabbitMQ...");
                            OnApplicationStart();
                        }
                    }, cts.Token);
                }
            }, cts.Token);
        }

        private void ProcessMessage(string message)
        {
            try
            {
                // 1. Deserialize JSON
                var data = JsonConvert.DeserializeObject<TTSMessage>(message);
                if (data == null || string.IsNullOrEmpty(data.tts))
                {
                    MelonLogger.Error("Invalid TTS message received.");
                    return;
                }

                // 2. Decode base64
                byte[] audioBytes = Convert.FromBase64String(data.tts);

                // 3. Save to WAV file
                try
                {
                    string saveDir = "TTS_CACHE";
                    Directory.CreateDirectory(saveDir);

                    string safeText = "tts_clip";

                    string filePath = Path.Combine(saveDir, $"{safeText}_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                    File.WriteAllBytes(filePath, audioBytes);

                    MelonLogger.Msg($"[RabbitMQ] Saved TTS to: {filePath}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Failed to save wav file: {ex.Message}");
                }

                // 4. play audio
                MelonCoroutines.Start(LoadMp3FromBytes(audioBytes, "RabbitMQ_TTS", (clip) =>
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }));
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error in ProcessMessage: {ex}");
                }
        }


        private IEnumerator LoadMp3FromBytes(byte[] mp3Data, string clipName, Action<AudioClip> onReady)
        {
            string tempPath = Path.Combine(Application.persistentDataPath, clipName + ".mp3");
            File.WriteAllBytes(tempPath, mp3Data);

            using (var uwr = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(uwr);
                    clip.name = clipName;
                    onReady?.Invoke(clip);
                }
                else
                {
                    MelonLogger.Error($"MP3 load failed: {uwr.error}");
                }
            }
        }

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("Shutting down RabbitMQ connection...");

            try
            {
                // Cancel any ongoing operations
                cts.Cancel();

                // Close channel and connection gracefully
                if (channel != null && channel.IsOpen)
                {
                    channel.Close();
                    channel.Dispose();
                }

                if (connection != null && connection.IsOpen)
                {
                    connection.Close();
                    connection.Dispose();
                }

                cts?.Dispose();

                MelonLogger.Msg("RabbitMQ connection closed successfully.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error during shutdown: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            // Optional: Add periodic connection health check
            // This runs every frame, so be careful with performance
        }
    }
}