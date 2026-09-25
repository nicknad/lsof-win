using Lsof.Models;

namespace Lsof.Output;

internal static class TableOutputWriter
{
    private const int ColumnPadding = 2; // Two spaces separate the fixed-width columns.

    public static void Write(IReadOnlyList<LsofEntry> entries)
    {
        ReadOnlySpan<string> headers = TableFormatter.Headers;
        int nameColumn = headers.Length - 1; // Name is the final, unpadded column.
        int[] widths = new int[nameColumn];
        for (int i = 0; i < widths.Length; i++)
        {
            widths[i] = headers[i].Length;
        }

        List<string[]> cells = new(entries.Count);
        foreach (LsofEntry entry in entries)
        {
            string[] rowCells = TableFormatter.GetCells(entry);
            cells.Add(rowCells);

            for (int i = 0; i < widths.Length; i++)
            {
                if (rowCells[i].Length > widths[i])
                {
                    widths[i] = rowCells[i].Length;
                }
            }
        }

        for (int i = 0; i < widths.Length; i++)
        {
            Console.Write(headers[i].PadRight(widths[i] + ColumnPadding));
        }
        Console.WriteLine(headers[nameColumn]);

        foreach (string[] rowCells in cells)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                Console.Write(rowCells[i].PadRight(widths[i] + ColumnPadding));
            }
            Console.WriteLine(rowCells[nameColumn]);
        }
    }
}
