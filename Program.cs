#define SDL
#define Console

using System.Diagnostics;
using Chip8Sharp;
using SDL2;

#if Console
Console.Title = "Chip8Sharp";
Console.CursorVisible = false;
#pragma warning disable CA1416 // Validate platform compatibility
Console.SetWindowSize(64, 33);
Console.SetBufferSize(64, 33);
#pragma warning restore CA1416 // Validate platform compatibility
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine(@"The following features are not available on a console window--
* User keyboard input
* Audio");
await Task.Delay(1000);
Console.Clear();
Console.ResetColor();
#endif
#if SDL
SDL.SDL_SetHint(SDL.SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
{
    throw new ApplicationException("SDL failed to init.");
}

IntPtr window = SDL.SDL_CreateWindow("Chip8Sharp", 128, 64, 64 * 8, 32 * 8, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
if (window == IntPtr.Zero)
{
    throw new ApplicationException("SDL could not create a window");
}
IntPtr renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
if (renderer == IntPtr.Zero)
{
    throw new ApplicationException("SDL could not create a valid renderer.");
}
#endif

#if SDL
var cpu = new CPU(window, renderer);
#elif Console
var cpu = new CPU();
#endif

using (var fs = new FileStream(@"Resources\roms\PONG2.ch8", FileMode.Open))
using (var reader = new BinaryReader(fs))
{
    var program = new List<byte>();
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

    while (true)
    {
        try
        {
            cpu.Step();
#if SDL
            cpu.PollEventsSDL();
#endif
        }
        catch (NotSupportedException e)
        {
            Debug.WriteLine(e.Message);
            break;
        }
    }
}
