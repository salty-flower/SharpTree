using System;
using ConsoleAppFramework;
using SharpTree;

var app = ConsoleApp.Create();
app.Add<Commands>();

GC.TryStartNoGCRegion(4 * 1024 * 1024);
app.Run(args);
