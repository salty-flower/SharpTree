using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppFramework;

namespace SharpTree;

public partial class TreeCommand
{
    private readonly StringBuilder sb = new();
    private int dirCount = 0;
    private int fileCount = 0;
    private int maxDepth = -1;
    private int currentDepth = 0;
    private bool includeFiles = false;
    private int flushInterval = 500;
    private readonly Stopwatch stopwatch = new();
    private BufferedStream bufferedOutput = null!;

    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="includeFiles">-f</param>
    /// <param name="maxDepth">-m</param>
    /// <param name="flushInterval">-t, Flush output to stdout every x ms. -1 to disable.</param>
    [Command("")]
    public async Task Tree(
        string path = ".",
        bool includeFiles = false,
        int maxDepth = -1,
        int flushInterval = 1
    )
    {
        this.maxDepth = maxDepth;
        this.includeFiles = includeFiles;
        this.flushInterval = flushInterval;
        path = Path.GetFullPath(path);

        using (
            bufferedOutput = new BufferedStream(Console.OpenStandardOutput(), Constants.BufferSize)
        )
        {
            InitializeOutput(path);
            stopwatch.Start();

            await DisplayTreeAsync(path, "");

            FinalizeOutput();
            await WriteBufferAsync();
        }
    }

    private async Task WriteBufferAsync()
    {
        var buffer = sb.ToString();
        await bufferedOutput.WriteAsync(Encoding.UTF8.GetBytes(buffer));
        await bufferedOutput.FlushAsync();
    }
}
