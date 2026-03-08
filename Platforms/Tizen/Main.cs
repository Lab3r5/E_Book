using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace E_Book;

public partial class Program : MauiApplication
{
    public Program() : base() { }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public static void Main(string[] args)
    {
        var app = new Program();
        app.Run(args);
    }
}