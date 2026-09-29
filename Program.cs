using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Spectre.Console;

class AppConfig
{
    public bool IsEnglish { get; set; } = false;
    public string LastSource { get; set; } = "Yandex Music";
    public float Volume { get; set; } = 1.0f; // 1.0 = 100%
}

class Program
{
    private const string AppName = "SoundBridge CLI";
    private const string Version = "2.2.0";
    private const string CableDownloadUrl = "https://vb-audio.com/Cable/";
    private const string ConfigFileName = "config.json";

    private static AppConfig Config = new();

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        LoadConfig();

        if (!File.Exists(ConfigFileName))
        {
            ChooseLanguage();
            SaveConfig();
        }

        if (!CheckVirtualCableExists())
        {
            RenderHeader();
            AnsiConsole.MarkupLine(Config.IsEnglish 
                ? "[bold red]❌ Error: Virtual Audio Cable not found in the system![/]\n" 
                : "[bold red]❌ Ошибка: Виртуальный аудиокабель не обнаружен в системе![/]\n");
            
            var panelText = Config.IsEnglish
                ? $"[cyan]{AppName}[/] requires a virtual audio cable (e.g., [yellow]VB-Audio Virtual Cable[/]).\n\n" +
                  $"1. Download it from the official website:\n   [link={CableDownloadUrl}]{CableDownloadUrl}[/]\n" +
                  $"2. Install it (administrator rights required).\n" +
                  $"3. Reboot your PC and run [cyan]{AppName}[/] again."
                : $"Для работы [cyan]{AppName}[/] необходим виртуальный аудиокабель (например, [yellow]VB-Audio Virtual Cable[/]).\n\n" +
                  $"1. Скачайте программу по официальной ссылке:\n   [link={CableDownloadUrl}]{CableDownloadUrl}[/]\n" +
                  $"2. Установите её (требуются права администратора).\n" +
                  $"3. Перезагрузите компьютер и запустите [cyan]{AppName}[/] снова.";

            var panel = new Panel(panelText)
            {
                Header = new PanelHeader(Config.IsEnglish ? " [yellow]Virtual Cable Required[/] " : " [yellow]Требуется установка Virtual Cable[/] "),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1, 2, 1)
            };
            
            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine(Config.IsEnglish ? "\n[dim]Press any key to exit...[/]" : "\n[dim]Нажмите любую клавишу для выхода...[/]");
            Console.ReadKey();
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            RenderHeader();

            var choices = Config.IsEnglish ? new[] {
                "🎧 Yandex Music",
                "🟢 Spotify",
                "🍎 iTunes",
                $"🔊 Volume / Громкость (Current: {Math.Round(Config.Volume * 100)}%)",
                "⚙️ Setup Guide",
                "🌐 Switch Language (RU/EN)",
                "❌ Exit"
            } : new[] {
                "🎧 Yandex Music",
                "🟢 Spotify",
                "🍎 iTunes",
                $"🔊 Настройка громкости (Текущая: {Math.Round(Config.Volume * 100)}%)",
                "⚙️ Инструкция по настройке",
                "🌐 Сменить язык (RU/EN)",
                "❌ Выход"
            };

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(Config.IsEnglish ? "[yellow]Select music source to stream into mic:[/]" : "[yellow]Выберите источник музыки для трансляции в микрофон:[/]")
                    .PageSize(10)
                    .AddChoices(choices));

            if (choice.Contains("Exit") || choice.Contains("Выход"))
            {
                SaveConfig();
                AnsiConsole.MarkupLine(Config.IsEnglish ? "[red]Shutting down. Goodbye![/]" : "[red]Завершение работы программы. До свидания![/]");
                break;
            }

            if (choice.Contains("Setup Guide") || choice.Contains("Инструкция"))
            {
                ShowHelp();
                continue;
            }

            if (choice.Contains("Switch Language") || choice.Contains("Сменить язык"))
            {
                Config.IsEnglish = !Config.IsEnglish;
                SaveConfig();
                continue;
            }

            if (choice.Contains("Volume") || choice.Contains("громкости"))
            {
                ConfigureVolume();
                continue;
            }

            string targetProcessName = choice switch
            {
                _ when choice.Contains("Yandex Music") => "Яндекс Музыка",
                _ when choice.Contains("Spotify") => "Spotify",
                _ when choice.Contains("iTunes") => "iTunes",
                _ => ""
            };

            Config.LastSource = choice;
            SaveConfig();

            RunRealStreamingSession(targetProcessName, choice);
        }
    }

    static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFileName))
            {
                string json = File.ReadAllText(ConfigFileName);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            Config = new AppConfig();
        }
    }

    static void SaveConfig()
    {
        try
        {
            string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFileName, json);
        }
        catch { }
    }

    static void ChooseLanguage()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("SoundBridge").Centered().Color(Color.Cyan1));
        AnsiConsole.Write(new Markup("[dim]SoundBridge CLI • Audio Bridge[/]\n").Centered());
        AnsiConsole.WriteLine();

        var langChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Choose your language / Выберите язык:[/]")
                .AddChoices(new[] { "🇷🇺 Русский", "🇬🇧 English" }));

        Config.IsEnglish = langChoice.Contains("English");
    }

    static void ConfigureVolume()
    {
        AnsiConsole.Clear();
        RenderHeader();

        int currentPercent = (int)(Config.Volume * 100);
        int newPercent = AnsiConsole.Prompt(
            new TextPrompt<int>(Config.IsEnglish ? "Enter volume level (0 - 200%):" : "Введите уровень громкости (0 - 200%):")
                .DefaultValue(currentPercent)
                .Validate(v => v >= 0 && v <= 200 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 0 and 200[/]")));

        Config.Volume = newPercent / 100f;
        SaveConfig();
    }

    static bool CheckVirtualCableExists()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            
            return devices.Any(d => 
                d.FriendlyName.Contains("Cable", StringComparison.OrdinalIgnoreCase) || 
                d.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    static void RenderHeader()
    {
        AnsiConsole.Write(
            new FigletText("SoundBridge")
                .Centered()
                .Color(Color.Cyan1));

        string subtitle = Config.IsEnglish 
            ? $"{AppName} v{Version} • Console audio bridge to virtual microphone" 
            : $"{AppName} v{Version} • Консольный аудиомост в виртуальный микрофон";
            
        AnsiConsole.Write(new Markup($"[dim]{subtitle}[/]").Centered());
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
    }

    static void RunRealStreamingSession(string processName, string serviceDisplayName)
    {
        AnsiConsole.Clear();
        RenderHeader();

        AnsiConsole.MarkupLine(Config.IsEnglish 
            ? $"[bold yellow]Looking for process:[/] [cyan]{processName}[/]" 
            : $"[bold yellow]Поиск процесса:[/] [cyan]{processName}[/]");

        Process? targetProcess = null;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start(Config.IsEnglish ? "Waiting for application to start..." : "Ожидание запуска приложения в системе...", ctx =>
            {
                string[] possibleNames = processName == "Яндекс Музыка" 
                    ? new[] { "Яндекс Музыка", "Yandex.Music", "YandexMusic" } 
                    : new[] { processName };

                while (targetProcess == null)
                {
                    foreach (var name in possibleNames)
                    {
                        targetProcess = Process.GetProcessesByName(name).FirstOrDefault();
                        if (targetProcess != null) break;
                    }

                    if (targetProcess == null)
                    {
                        Thread.Sleep(1000);
                    }
                }
            });

        AnsiConsole.MarkupLine(Config.IsEnglish 
            ? $"[green]✔ Connected to process! (PID: {targetProcess.Id})[/]\n" 
            : $"[green]✔ Успешное подключение к процессу! (PID: {targetProcess.Id})[/]\n");

        var enumerator = new MMDeviceEnumerator();
        var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

        var cableDevice = renderDevices.FirstOrDefault(d => 
            d.FriendlyName.Contains("Cable Output", StringComparison.OrdinalIgnoreCase) || 
            d.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));

        MMDevice selectedDevice;
        if (cableDevice == null)
        {
            var deviceNames = renderDevices.Select(d => d.FriendlyName).ToList();
            deviceNames.Add(Config.IsEnglish ? "Back to menu" : "Назад в меню");

            var selectedName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(Config.IsEnglish ? "[yellow]Select Virtual Cable destination device:[/]" : "[yellow]Выберите устройство Virtual Cable назначения:[/]")
                    .AddChoices(deviceNames));

            if (selectedName.Contains("Back") || selectedName.Contains("Назад")) return;
            selectedDevice = renderDevices.First(d => d.FriendlyName == selectedName);
        }
        else
        {
            selectedDevice = cableDevice;
        }

        AnsiConsole.Clear();
        RenderHeader();

        var table = new Table();
        table.AddColumn(Config.IsEnglish ? "Parameter" : "Параметр");
        table.AddColumn(Config.IsEnglish ? "Status" : "Статус");
        table.AddRow(Config.IsEnglish ? "Source" : "Источник", $"[cyan]{serviceDisplayName}[/]");
        table.AddRow(Config.IsEnglish ? "Target Process" : "Целевой процесс", $"{processName} (PID: {targetProcess.Id})");
        table.AddRow(Config.IsEnglish ? "Virtual Cable" : "Виртуальный кабель", $"[green]{selectedDevice.FriendlyName}[/]");
        table.AddRow(Config.IsEnglish ? "Volume" : "Громкость", $"[yellow]{Math.Round(Config.Volume * 100)}%[/]");
        table.AddRow(Config.IsEnglish ? "Stream Status" : "Статус потока", "[green]Active / Активен (Process Loopback)[/]");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine(Config.IsEnglish 
            ? "\n[yellow]💡 Tip:[/] In Discord/Telegram/Game settings, select [green]CABLE Output[/] as your microphone."
            : "\n[yellow]💡 Совет:[/] В настройках Discord/Telegram/игры выберите [green]CABLE Output[/] в качестве вашего микрофона.");
        
        AnsiConsole.MarkupLine(Config.IsEnglish 
            ? "\n[red]Press Ctrl + C or close target app to stop streaming.[/]"
            : "\n[red]Нажмите Ctrl + C или закройте музыкальное приложение для остановки.[/]");

        try
        {
            // Используем асинхронный билдер, так как процессный лупбек инициализируется асинхронно
            using var recorder = new WasapiRecorderBuilder()
                .WithProcessLoopback((uint)targetProcess.Id)
                .BuildAsync()
                .GetAwaiter()
                .GetResult();

            using var outClient = new WasapiOut(selectedDevice, AudioClientShareMode.Shared, true, 100);

            var provider = new BufferedWaveProvider(recorder.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            recorder.DataAvailable += (buffer, flags, devicePosition, qpcPosition) =>
            {
                if (buffer.Length > 0)
                {
                    byte[] data = buffer.ToArray();

                    if (Config.Volume != 1.0f)
                    {
                        ApplyVolume(data, data.Length, Config.Volume, recorder.WaveFormat.BitsPerSample);
                    }
                    provider.AddSamples(data, 0, data.Length);
                }
            };

            outClient.Init(provider);
            recorder.StartRecording();
            outClient.Play();

            while (!targetProcess.HasExited)
            {
                Thread.Sleep(500);
            }

            recorder.StopRecording();
            outClient.Stop();

            AnsiConsole.MarkupLine(Config.IsEnglish ? "\n[yellow]⚠️ Target application was closed.[/]" : "\n[yellow]⚠️ Целевое приложение было завершено.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(Config.IsEnglish ? $"\n[red]Audio streaming error: {ex.Message}[/]" : $"\n[red]Ошибка потоковой передачи звука: {ex.Message}[/]");
        }

        AnsiConsole.MarkupLine(Config.IsEnglish ? "\n[dim]Press any key to return to menu...[/]" : "\n[dim]Нажмите любую клавишу для возврата в меню...[/]");
        Console.ReadKey();
    }

    static void ApplyVolume(byte[] buffer, int bytesRecorded, float volume, int bitsPerSample)
    {
        if (bitsPerSample == 16)
        {
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                int adjusted = (int)(sample * volume);
                if (adjusted > short.MaxValue) adjusted = short.MaxValue;
                if (adjusted < short.MinValue) adjusted = short.MinValue;
                byte[] bytes = BitConverter.GetBytes((short)adjusted);
                buffer[i] = bytes[0];
                buffer[i + 1] = bytes[1];
            }
        }
        else if (bitsPerSample == 32)
        {
            for (int i = 0; i < bytesRecorded; i += 4)
            {
                float sample = BitConverter.ToSingle(buffer, i);
                float adjusted = sample * volume;
                byte[] bytes = BitConverter.GetBytes(adjusted);
                Buffer.BlockCopy(bytes, 0, buffer, i, 4);
            }
        }
    }

    static void ShowHelp()
    {
        AnsiConsole.Clear();
        RenderHeader();

        string helpText = Config.IsEnglish
            ? $"1. Download [cyan]VB-Audio Virtual Cable[/] from: [link={CableDownloadUrl}]{CableDownloadUrl}[/]\n" +
              "2. Install it and reboot your computer.\n" +
              $"3. In [cyan]{AppName}[/], choose your music player and the app will auto-connect to it.\n" +
              "4. In your voice chat (Discord, games), select [green]CABLE Output[/] as your microphone.\n\n" +
              "Now other participants will hear music straight from your chosen app through your virtual mic."
            : $"1. Скачайте [cyan]VB-Audio Virtual Cable[/] с сайта: [link={CableDownloadUrl}]{CableDownloadUrl}[/]\n" +
              "2. Установите его и обязательно перезагрузите компьютер.\n" +
              $"3. В [cyan]{AppName}[/] выберите ваш музыкальный плеер, и программа сама подключится к нему.\n" +
              "4. В голосовом чате (Discord, игры) в качестве вашего микрофона выберите [green]CABLE Output[/].\n\n" +
              "Теперь собеседники будут слышать музыку из выбранного приложения прямо через ваш виртуальный микрофон.";

        var panel = new Panel(helpText)
        {
            Header = new PanelHeader(Config.IsEnglish ? " [yellow]Setup Guide[/] " : " [yellow]Инструкция по настройке[/] "),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1, 2, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine(Config.IsEnglish ? "\n[dim]Press any key to return to menu...[/]" : "\n[dim]Нажмите любую клавишу для возврата в меню...[/]");
        Console.ReadKey();
    }
}