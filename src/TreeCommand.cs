using System;
using System.IO;
using ConsoleAppFramework;

namespace SharpTree;

public class TreeCommand
{
    /// <summary>
    /// Display a tree of the specified directory.
    /// </summary>
    /// <param name="path">-p, Root path of the tree</param>
    /// <param name="includeFiles">-f</param>
    /// <param name="maxDepth">-m</param>
    /// <param name="flushInterval">-t, Flush output to stdout every x ms. -1 to disable.</param>
    [Command("")]
    public void Tree(
        string path = ".",
        bool includeFiles = false,
        int maxDepth = -1,
        int flushInterval = -1
    )
    {
        path = Path.GetFullPath(path);

        using var output = Console.OpenStandardOutput();
        var renderer = new TreeRenderer(output, flushInterval);
        renderer.WriteHeader(path);
        renderer.Start();

        var walker = new ManagedTreeWalker();

        var (dirs, files) = walker.Walk(path, includeFiles, maxDepth, renderer);

        renderer.WriteFooter(dirs, files);
        renderer.Flush();
    }
}
