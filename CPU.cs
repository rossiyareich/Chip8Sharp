using System.Diagnostics;
using SDL2;

namespace Chip8Sharp
{
    public class CPU
    {
        public byte[] RAM = new byte[4096];         //4kb RAM
        public byte[] V = new byte[16];             //16 data registers (V0 => VF), 8 bits wide each
        public ushort PC = 0;                       //PC, 12 bits wide
        public ushort I = 0;                        //16 bit register for MEM
        public Stack<ushort> Stack = new(24);       //24 Levels of nesting, 48 bytes wide total, 2 bytes wide each
        public byte DelayTimer = 0;                 //Delay timer, 8 bits wide
        public byte SoundTimer = 0;                 //Sound timer, 8 bits wide
        public byte Keyboard = 0;                   //Keyboard input, using only lower 4 bits for 16 keys total
        public uint[] Display = new uint[64 * 32];  //64x32 Monochromatic display, one byte per pixel (on/off)

        Random rng = new();
        Stopwatch watch = new();
        public bool IsWaitingForKeyPress = false;

        void InitializeFont()
        {
            byte[] characters = new byte[]
            {
                0xF0,
                0x90,
                0x90,
                0x90,
                0xF0,
                0x20,
                0x60,
                0x20,
                0x20,
                0x70,
                0xF0,
                0x10,
                0xF0,
                0x80,
                0xF0,
                0xF0,
                0x10,
                0xF0,
                0x10,
                0xF0,
                0x90,
                0x90,
                0xF0,
                0x10,
                0x10,
                0xF0,
                0x80,
                0xF0,
                0x10,
                0xF0,
                0xF0,
                0x80,
                0xF0,
                0x90,
                0xF0,
                0xF0,
                0x10,
                0x20,
                0x40,
                0x40,
                0xF0,
                0x90,
                0xF0,
                0x90,
                0xF0,
                0xF0,
                0x90,
                0xF0,
                0x10,
                0xF0,
                0xF0,
                0x90,
                0xF0,
                0x90,
                0x90,
                0xE0,
                0x90,
                0xE0,
                0x90,
                0xE0,
                0xF0,
                0x80,
                0x80,
                0x80,
                0xF0,
                0xE0,
                0x90,
                0x90,
                0x90,
                0xE0,
                0xF0,
                0x80,
                0xF0,
                0x80,
                0xF0,
                0xF0,
                0x80,
                0xF0,
                0x80,
                0x80
            };
            Array.Copy(characters, RAM, characters.Length);
        }

        public void DrawDisplay()
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
            for (var y = 0; y < 32; y++)
            {
                string line = "";
                for (var x = 0; x < 64; x++)
                {
                    if (Display[x + (y * 64)] != 0)
                    {
                        line += "*";
                    }
                    else
                    {
                        line += " ";
                    }
                }
                Console.WriteLine(line);
            }
            Thread.Sleep(1);
        }

        public void LoadProgram(IEnumerable<byte> program)
        {
            InitializeFont();
            var programArr = program.ToArray();
            for (var i = 0; i < programArr.Length; i++)
            {
                RAM[512 + i] = programArr[i];
            };
            PC = 512;
        }

        public void Step()
        {
            if (!watch.IsRunning)
            {
                watch.Start();
            }
            if (watch.ElapsedMilliseconds > 100 / 6)
            {
                if (DelayTimer > 0)
                {
                    DelayTimer--;
                }

                if (SoundTimer > 0)
                {
                    SoundTimer--;
                }

                watch.Restart();
            }

            var opcode = (ushort)((RAM[PC] << 8) | RAM[PC + 1]);

            if (IsWaitingForKeyPress)
            {
                V[(opcode & 0x0F00 >> 8)] = Keyboard;
                return;
            }

            ushort firstNibble = (ushort)(opcode & 0xF000);     //Get the top nibble

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
                                V[0xF] = (byte)(((V[x] & 0x80) == 0x80) ? 1 : 0);
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
                    PC = (ushort)(opcode & 0x0FFF + V[0]);
                    break;
                case 0xC000:
                    V[(opcode & 0x0F00) >> 8] = (byte)(rng.Next(0, 256) & (opcode & 0x00FF));
                    break;
                case 0xD000:
                    {
                        int x = V[(opcode & 0x0F00) >> 8];
                        int y = V[(opcode & 0x00F0) >> 4];
                        int n = opcode & 0x000F;

                        V[0xF] = 0;

                        for (var row = 0; row < n; row++)
                        {
                            byte mem = RAM[I + row];
                            for (var cursor = 0; cursor < 8; cursor++)
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

                                Display[index] = ((Display[index] != 0 && pixel == 0) || (Display[index] == 0 && pixel == 1)) ? 0xFFFFFFFF : 0;
                            }
                        }
                        DrawDisplay();
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
                                if(V[x] >= 0)
                                    RAM[I] = byte.Parse(V[x].ToString()[0].ToString());
                                if(V[x] > 9)
                                    RAM[I + 1] = byte.Parse(V[x].ToString()[1].ToString());
                                if(V[x] > 99)
                                    RAM[I + 2] = byte.Parse(V[x].ToString()[2].ToString());
                                break;
                            case 0x55:
                                for (var i = 0; i <= x; i++)
                                {
                                    RAM[I + i] = V[i];
                                }
                                break;
                            case 0x65:
                                for (var i = 0; i <= x; i++)
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

        void ThrowOpcode(int @case, ushort opcode) => throw @case switch
        {
            1 => new NotSupportedException($"Unimplemented opcode {opcode.ToString("X4")}"),
            2 => new NotSupportedException($"Unsupported opcode {opcode.ToString("X4")}"),
            _ => new NotSupportedException($"Exception case {@case} on opcode {opcode.ToString("X4")}")
        };
    }
}
