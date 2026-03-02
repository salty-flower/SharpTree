using System;
using ConsoleAppFramework;
using SharpTree;

var app = ConsoleApp.Create();
app.Add<TreeCommand>();

app.Run(args);
