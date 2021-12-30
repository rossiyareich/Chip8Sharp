using System.Diagnostics;
using Chip8Sharp;
using SDL2;

SDL.SDL_SetHint(SDL.SDL_HINT_WINDOWS_DISABLE_THREAD_NAMING, "1");
if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
{
    throw new ApplicationException("SDL failed to init.");
}

IntPtr window = SDL.SDL_CreateWindow("Chip8Sharp", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, 64 * 8 * 2, 32 * 8 * 2, 0);
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

SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);
var texturePng = SDL_image.IMG_LoadTexture(renderer, "Resources/Keymapping.png");
SDL.SDL_RenderCopy(renderer, texturePng, IntPtr.Zero, IntPtr.Zero);
SDL.SDL_RenderPresent(renderer);
SDL_image.IMG_Quit();

if (texturePng != IntPtr.Zero)
    SDL.SDL_DestroyTexture(texturePng);

Stopwatch watch = new();
watch.Start();
while (SDL.SDL_WaitEvent(out var sdlEvent) != 0 && watch.ElapsedMilliseconds < 2000)
{
    if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
    {
        Environment.Exit(0);
    }
}
watch.Stop();

SDL.SDL_RenderClear(renderer);
SDL.SDL_RenderPresent(renderer);
var cpu = new CPU(window, renderer);

using (cpu)
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
            cpu.PollEventsSDL();
        }
        catch (NotSupportedException e)
        {
            Debug.WriteLine(e.Message);
            break;
        }
    }
}
