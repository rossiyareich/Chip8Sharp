using Chip8Sharp;

Console.Title = "Chip8Sharp";
Console.CursorVisible = false;
#pragma warning disable CA1416 // Validate platform compatibility
Console.SetWindowSize(64, 32);
#pragma warning restore CA1416 // Validate platform compatibility
Console.SetBufferSize(64, 32);

var cpu = new CPU();

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
        }
        catch (NotSupportedException e)
        {
            Console.WriteLine(e.Message);
            break;
        }
    }
}

await Task.Delay(-1);
