# CognigyCSharpClient
Repo for the Cognigy (server) client which can be used 
to connect to the Cognigy Brain from C# applications.

## Installation
Add the CognigyClientCSharp project to to your project and restore the nuGet packages.

## Usage
```cs
using Cognigy;

public partial class MainWindow : Window
{
    CognigyClient client;

    public MainWindow()
    {
        Options options = new Options
        {
            baseUrl = "exampleUrl",
            user = "exampleUser",
            flow = "smalltalk",
            apikey = "exampleApiKey"
        };

        client = new CognigyClient(options);
        client.Connect().ContinueWith((t) =>
        {
            client.OnOutput += OnResponse;
            client.SendMessage("Hi");
        });
    }

    private void OnResponse(object sender, OutputEventArgs args)
    {
        Console.WriteLine("Response: " + args.Output.text);
    }
}
```
