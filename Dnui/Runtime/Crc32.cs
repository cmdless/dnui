namespace Dnui.Acquisition;

/// <summary>CRC-32 (IEEE 802.3), as recorded per entry in zip/nupkg archives.</summary>
internal static class Crc32
{
    private static readonly uint[] s_table = CreateTable();

    public static uint Compute(Stream stream)
    {
        var crc = 0xFFFFFFFFu;
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                crc = s_table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
