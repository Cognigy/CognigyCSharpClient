# CognigyCSharpClient
Repo for the Cognigy (server) client which can be used 
to connect to the COGNIGY.AI platform from a C# application.

## Installation
Add the CognigyClientCSharp project to to your project and restore the nuGet packages. You'll need the following packages:

- SocketIoClientDotNet
- Newtonsoft.Json

## Usage
The following example uses the CognigyCSharpClient in a simple WPF app.

```cs
using Cognigy;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Options options = new Options()
            {
                endpointURL = "<ENDPOINT_URL>",
                URLToken = "<URL_TOKEN>",
                sessionId = "<SESSION_ID>",
                userId = "<USER_ID>",

                // You have to define this for handling the outputs of the Flow on COGNIGY.AI
                handleOutput = HandleOutput,

                // Optional Parameters
                channel = "<CHANNEL>",

                keepMarkup = true,

                resetState = true,
                resetContext = false,
                reloadFlow = false,
                resetFlow = true,

                handleError = HandleError,
                handleException = HandleException,
                handlePing = HandlePing,

                passthroughIP = "<PASSTHROUGH_IP>"
            };

            CognigyClient cognigyClient = new CognigyClient(options);


            cognigyClient.Connect().ContinueWith((t) =>
            {
                // Simple text messages
                cognigyClient.SendMessage("text message");

                // Or data
                cognigyClient.SendMessage(null, new { key = "value" });
            });
        }

        private void HandleError(ErrorResponse error)
        {
            Console.WriteLine(error.message);
        }

        private void HandleException(ErrorResponse exception)
        {
            Console.WriteLine(exception.message);
        }

        private void HandlePing(FinalPing finalPing)
        {
            Console.WriteLine(finalPing.type);
        }

        private void HandleOutput(FlowResponse response)
        {
            Console.WriteLine(response.text);
        }
    }
```
