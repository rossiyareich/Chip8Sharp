using System.Diagnostics;
using SDL2;

namespace Chip8Sharp;

public class CPU : IDisposable
{
    private readonly IntPtr _renderer;

    private readonly Random _rng = new();
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    private readonly IntPtr _window;
    public byte DelayTimer; //Delay timer, 8 bits wide
    public uint[] Display = new uint[64 * 32]; //64x32 Monochromatic display, one byte per pixel (on/off)
    public ushort I; //16 bit register for MEM (or a global pointer, as you may)

    public bool IsWaitingForKeyPress;
    public ushort Keyboard; //Keyboard input, using only lower 4 bits for 16 keys total
    public ushort PC; //PC, 12 bits wide
    public byte[] RAM = new byte[4096]; //4kb RAM

    private IntPtr _sdlSurface;
    private IntPtr _sdlTexture;
    public byte SoundTimer; //Sound timer, 8 bits wide

    public Stack<ushort>
        Stack = new(24); //24 Levels of nesting, 48 bytes wide total, 2 bytes wide each for program instructions

    public byte[] V = new byte[16]; //16 data registers (V0 => VF), 8 bits wide each

    public CPU(IntPtr window, IntPtr renderer)
    {
        _window = window;
        _renderer = renderer;
    }

    public void Dispose()
    {
        SDL.SDL_DestroyRenderer(_renderer);
        SDL.SDL_DestroyWindow(_window);
    }

    private void InitializeFont()
    {
        byte[] characters =
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, 0x20, 0x60, 0x20, 0x20, 0x70, 0xF0, 0x10, 0xF0, 0x80, 0xF0, 0xF0, 0x10,
            0xF0, 0x10, 0xF0, 0x90, 0x90, 0xF0, 0x10, 0x10, 0xF0, 0x80, 0xF0, 0x10, 0xF0, 0xF0, 0x80, 0xF0, 0x90,
            0xF0, 0xF0, 0x10, 0x20, 0x40, 0x40, 0xF0, 0x90, 0xF0, 0x90, 0xF0, 0xF0, 0x90, 0xF0, 0x10, 0xF0, 0xF0,
            0x90, 0xF0, 0x90, 0x90, 0xE0, 0x90, 0xE0, 0x90, 0xE0, 0xF0, 0x80, 0x80, 0x80, 0xF0, 0xE0, 0x90, 0x90,
            0x90, 0xE0, 0xF0, 0x80, 0xF0, 0x80, 0xF0, 0xF0, 0x80, 0xF0, 0x80, 0x80
        };
        Array.Copy(characters, RAM, characters.Length);
    }

    /*
        1  2  3  C         1  2  3  4
        4  5  6  D   ===   Q  W  E  R
        7  8  9  E   ===   A  S  D  F
        A  0  B  F         Z  X  C  V

        The '8', '4', '6', and '2' keys are typically used for directional input.
    */
    private static int KeyCodeToKeyIndex(SDL.SDL_Keycode keycode) => keycode switch
    {
        SDL.SDL_Keycode.SDLK_1 => 0x1,
        SDL.SDL_Keycode.SDLK_2 => 0x2,
        SDL.SDL_Keycode.SDLK_3 => 0x3,
        SDL.SDL_Keycode.SDLK_4 => 0xC,
        SDL.SDL_Keycode.SDLK_q => 0x4,
        SDL.SDL_Keycode.SDLK_w => 0x5,
        SDL.SDL_Keycode.SDLK_e => 0x6,
        SDL.SDL_Keycode.SDLK_r => 0xD,
        SDL.SDL_Keycode.SDLK_a => 0x7,
        SDL.SDL_Keycode.SDLK_s => 0x8,
        SDL.SDL_Keycode.SDLK_d => 0x9,
        SDL.SDL_Keycode.SDLK_f => 0xE,
        SDL.SDL_Keycode.SDLK_z => 0xA,
        SDL.SDL_Keycode.SDLK_x => 0x0,
        SDL.SDL_Keycode.SDLK_c => 0xB,
        SDL.SDL_Keycode.SDLK_v => 0xF,
        _ => -1
    };

    public void PollEventsSDL()
    {
        while (SDL.SDL_PollEvent(out SDL.SDL_Event sdlEvent) != 0)
        {
            switch (sdlEvent.type)
            {
                case SDL.SDL_EventType.SDL_QUIT:
                    Dispose();
                    Environment.Exit(0);
                    break;
                case SDL.SDL_EventType.SDL_KEYDOWN:
                    {
                        int key = KeyCodeToKeyIndex(sdlEvent.key.keysym.sym);
                        if (key != -1)
                        {
                            Keyboard |= (ushort)(1 << key);
                        }

                        if (IsWaitingForKeyPress)
                        {
                            KeyPressed((byte)key);
                        }
                    }
                    break;
                case SDL.SDL_EventType.SDL_KEYUP:
                    {
                        int key = KeyCodeToKeyIndex(sdlEvent.key.keysym.sym);
                        if (key != -1)
                        {
                            Keyboard &= (ushort)~(1 << key);
                        }
                    }
                    break;
            }
        }
    }

    private void KeyPressed(byte key)
    {
        IsWaitingForKeyPress = false;
        ushort opcode = (ushort)((RAM[PC] << 8) | RAM[PC + 1]);
        V[opcode & (0x0F00 >> 8)] = key;
        PC += 2;
    }

    public void DrawDisplaySDL()
    {
        unsafe
        {
            fixed (uint* displayHandle = Display)
            {
                _sdlSurface = SDL.SDL_CreateRGBSurfaceFrom(new IntPtr(displayHandle), 64, 32, 32, 64 * 4, 0xFF000000,
                    0x00FF0000, 0x0000FF00, 0x000000FF);
            }
        }

        _sdlTexture = SDL.SDL_CreateTextureFromSurface(_renderer, _sdlSurface);
        SDL.SDL_RenderClear(_renderer);
        SDL.SDL_RenderCopy(_renderer, _sdlTexture, IntPtr.Zero, IntPtr.Zero);
        SDL.SDL_RenderPresent(_renderer);

        if (_sdlSurface != IntPtr.Zero)
        {
            SDL.SDL_FreeSurface(_sdlSurface);
        }

        if (_sdlTexture != IntPtr.Zero)
        {
            SDL.SDL_DestroyTexture(_sdlTexture);
        }

        SDL.SDL_Delay(10);
    }

    public void LoadProgram(IEnumerable<byte> program)
    {
        InitializeFont();
        byte[] programArr = program.ToArray();
        for (int i = 0; i < programArr.Length; i++)
        {
            RAM[512 + i] = programArr[i];
        }

        PC = 512;
    }

    public void Step()
    {
        if (_watch.ElapsedMilliseconds > 100 / 6)
        {
            PollEventsSDL();
            if (DelayTimer > 0)
            {
                DelayTimer--;
            }

            if (SoundTimer > 0)
            {
                SoundTimer--;
            }

            _watch.Restart();
        }

        ushort opcode = (ushort)((RAM[PC] << 8) | RAM[PC + 1]);

        if (IsWaitingForKeyPress)
        {
            throw new Exception("Step should not be called when IsWaitingForKeyPress is true");
        }

        ushort firstNibble = (ushort)(opcode & 0xF000); //Get the top nibble

        PC += 2;

        switch (firstNibble)
        {
            case 0x0000:
                if (opcode == 0x00E0)
                {
                    Array.Clear(Display);
                }
                else if (opcode == 0x00EE)
                {
                    PC = Stack.Pop();
                }
                else
                {
                    ThrowOpcode(1, opcode); //0nnn is ignored
                }

                break;
            case 0x1000:
                PC = (ushort)(opcode & 0x0FFF);
                break;
            case 0x2000:
                Stack.Push(PC);
                PC = (ushort)(opcode & 0x0FFF);
                break;
            case 0x3000:
                if (V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF))
                {
                    PC += 2;
                }

                break;
            case 0x4000:
                if (V[(opcode & 0x0F00) >> 8] != (opcode & 0x00FF))
                {
                    PC += 2;
                }

                break;
            case 0x5000:
                if ((opcode & 0x000F) != 0)
                {
                    ThrowOpcode(2, opcode);
                }
                else if (V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4])
                {
                    PC += 2;
                }

                break;
            case 0x6000:
                V[(opcode & 0x0F00) >> 8] = (byte)(opcode & 0x00FF);
                break;
            case 0x7000:
                V[(opcode & 0x0F00) >> 8] += (byte)(opcode & 0x00FF);
                break;
            case 0x8000:
                {
                    int x = (opcode & 0x0F00) >> 8;
                    int y = (opcode & 0x00F0) >> 4;
                    switch (opcode & 0x000F)
                    {
                        case 0:
                            V[x] = V[y];
                            break;
                        case 1:
                            V[x] |= V[y];
                            break;
                        case 2:
                            V[x] &= V[y];
                            break;
                        case 3:
                            V[x] ^= V[y];
                            break;
                        case 4:
                            V[0xF] = (byte)(V[x] + V[y] > 0xFF ? 1 : 0);
                            V[x] = (byte)((V[x] + V[y]) & 0x00FF);
                            break;
                        case 5:
                            V[0xF] = (byte)(V[x] > V[y] ? 1 : 0);
                            V[x] = (byte)((V[x] - V[y]) & 0x00FF);
                            break;
                        case 6:
                            V[0xF] = (byte)(V[x] & 0x0001);
                            V[x] >>= 1;
                            break;
                        case 7:
                            V[0xF] = (byte)(V[y] > V[x] ? 1 : 0);
                            V[x] = (byte)((V[y] - V[x]) & 0x00FF);
                            break;
                        case 0xE:
                            V[0xF] = (byte)((V[x] & 0x80) == 0x80 ? 1 : 0);
                            V[x] <<= 1;
                            break;
                        default:
                            ThrowOpcode(2, opcode);
                            break;
                    }
                }
                break;
            case 0x9000:
                if ((opcode & 0x000F) != 0)
                {
                    ThrowOpcode(2, opcode);
                }

                if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00F0) >> 4])
                {
                    PC += 2;
                }

                break;
            case 0xA000:
                I = (ushort)(opcode & 0x0FFF);
                break;
            case 0xB000:
                PC = (ushort)(opcode & (0x0FFF + V[0]));
                break;
            case 0xC000:
                V[(opcode & 0x0F00) >> 8] = (byte)(_rng.Next(0, 256) & opcode & 0x00FF);
                break;
            case 0xD000:
                {
                    int x = V[(opcode & 0x0F00) >> 8];
                    int y = V[(opcode & 0x00F0) >> 4];
                    int n = opcode & 0x000F;

                    V[0xF] = 0;

                    for (int row = 0; row < n; row++)
                    {
                        byte mem = RAM[I + row];
                        for (int cursor = 0; cursor < 8; cursor++)
                        {
                            byte pixel = (byte)((mem >> (7 - cursor)) & 0x01);
                            int index = x + cursor + (y + row) * 64;

                            if (index > 0x7FF)
                            {
                                continue;
                            }

                            if (pixel == 1 && Display[index] != 0)
                            {
                                V[0xF] = 1;
                            }

                            Display[index] = Display[index] != 0 && pixel == 0 || Display[index] == 0 && pixel == 1
                                ? 0x7797C9FFu
                                : 0x00000000;
                        }
                    }

                    DrawDisplaySDL();
                }
                break;
            case 0xE000:
                if ((opcode & 0x00FF) == 0x009E)
                {
                    if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) == 0x01)
                    {
                        PC += 2;
                    }
                }
                else if ((opcode & 0x00FF) == 0x00A1)
                {
                    if (((Keyboard >> V[(opcode & 0x0F00) >> 8]) & 0x01) != 0x01)
                    {
                        PC += 2;
                    }
                }
                else
                {
                    ThrowOpcode(2, opcode);
                }

                break;
            case 0xF000:
                {
                    int x = (opcode & 0x0F00) >> 8;
                    switch (opcode & 0x00FF)
                    {
                        case 0x07:
                            V[x] = DelayTimer;
                            break;
                        case 0x0A:
                            IsWaitingForKeyPress = true;
                            PC -= 2;
                            break;
                        case 0x15:
                            DelayTimer = V[x];
                            break;
                        case 0x18:
                            SoundTimer = V[x];
                            break;
                        case 0x1E:
                            I += V[x];
                            break;
                        case 0x29:
                            I = (ushort)(V[x] * 5);
                            break;
                        case 0x33:
                            RAM[I] = (byte)(V[x] / 100);
                            RAM[I + 1] = (byte)(V[x] % 100 / 10);
                            RAM[I + 2] = (byte)(V[x] % 10);
                            break;
                        case 0x55:
                            for (int i = 0; i <= x; i++)
                            {
                                RAM[I + i] = V[i];
                            }

                            break;
                        case 0x65:
                            for (int i = 0; i <= x; i++)
                            {
                                V[i] = RAM[I + i];
                            }

                            break;
                        default:
                            ThrowOpcode(2, opcode);
                            break;
                    }
                }
                break;
            default:
                ThrowOpcode(1, opcode);
                break;
        }
    }

    private void ThrowOpcode(int @case, ushort opcode) => throw @case switch
    {
        1 => new NotSupportedException($"Unimplemented opcode {opcode:X4}"),
        2 => new NotSupportedException($"Unsupported opcode {opcode:X4}"),
        _ => new NotSupportedException($"Exception case {@case} on opcode {opcode:X4}")
    };
}
