using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SDL2;

namespace Chip8Sharp;

internal class Program
{
    private const string JsonPath = "config.json";

    private static void Main()
    {
        Configuration configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(JsonPath));

        SDL.SDL_SetHint(SDL.SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
        if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
        {
            throw new ApplicationException("SDL failed to init.");
        }

        IntPtr window = SDL.SDL_CreateWindow("Chip8Sharp", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            64 * 8 * 2, 32 * 8 * 2, 0);
        if (window == IntPtr.Zero)
        {
            throw new ApplicationException("SDL could not create a window");
        }

        IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if (renderer == IntPtr.Zero)
        {
            throw new ApplicationException("SDL could not create a valid renderer.");
        }

        SDL.SDL_RenderClear(renderer);
        SDL.SDL_RenderPresent(renderer);

        if (configuration is { ShowKeymapping: true })
        {
            SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);
            IntPtr texturePng = SDL_image.IMG_LoadTexture(renderer, "Resources/Keymapping.png");
            SDL.SDL_RenderCopy(renderer, texturePng, IntPtr.Zero, IntPtr.Zero);
            SDL.SDL_RenderPresent(renderer);
            SDL_image.IMG_Quit();

            if (texturePng != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(texturePng);
            }

            Stopwatch watch = new();
            watch.Start();
            while (SDL.SDL_WaitEvent(out SDL.SDL_Event sdlEvent) != 0 && watch.ElapsedMilliseconds < 2000)
            {
                if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                {
                    Environment.Exit(0);
                }
            }

            watch.Stop();

            SDL.SDL_RenderClear(renderer);
            SDL.SDL_RenderPresent(renderer);
        }

        CPU cpu = new(window, renderer);

        using (cpu)
        using (FileStream fs = new(
                   string.IsNullOrEmpty(configuration?.FilePath)
                       ? throw new FileNotFoundException()
                       : configuration.FilePath, FileMode.Open))
        using (BinaryReader reader = new(fs))
        {
            List<byte> program = new();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                try
                {
                    program.Add(reader.ReadByte());
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            cpu.LoadProgram(program);

            WaveOutEvent waveOut = new();
            Task.Run(() =>
            {
                SignalGenerator beep = new() { Frequency = 800, Gain = 0.2 };
                using (waveOut)
                {
                    while (true)
                    {
                        if (cpu.SoundTimer > 0)
                        {
                            waveOut.Init(beep);
                            waveOut.Play();
                            while (waveOut.PlaybackState == PlaybackState.Playing)
                            {
                            }
                        }
                    }
                }
            });

            while (true)
            {
                try
                {
                    if (!cpu.IsWaitingForKeyPress)
                    {
                        cpu.Step();
                    }

                    if (cpu.SoundTimer <= 0)
                    {
                        waveOut.Stop();
                    }
                }
                catch (NotSupportedException e)
                {
                    Debug.WriteLine(e.Message);
                    break;
                }
            }
        }
    }
}
