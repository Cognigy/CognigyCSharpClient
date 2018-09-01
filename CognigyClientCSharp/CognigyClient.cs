using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cognigy
{
    public class CognigyClient
    {
        public event EventHandler<OutputEventArgs> OnOutput;

        private Options options;
        private Socket mySocket;
        private bool firstLoad;

        private bool connected = false;

        private Action<string, string> LogError = (type, message) => Console.Error.WriteLine(string.Format("-- {0} -- \n{1} \n", type, message));
        private Action<string, string> LogStatus = (status, message) => Console.WriteLine(string.Format("--{0}: {1} --\n", status, message));

        private Func<string, Output> DeserializeToOutput = (output) => JsonConvert.DeserializeObject<Output>(output);

        private static AutoResetEvent waitHandle = new AutoResetEvent(false);

        public CognigyClient(Options options)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;

            this.options = options;
            this.firstLoad = true;
        }

        public async Task Connect()
        {
            string token = await GetToken(this.options.baseUrl, this.options.user, this.options.apikey, this.options.channel, this.options.token);
            Socket socket = await EstablishSocketConnection(token);
            await InitializeCognigyClient(socket);
        }

        /// <summary>
        /// Checks if the passed token is valid. In case it's null or empty a new token gets fetched
        /// </summary>
        /// <param name="baseUrl">URI of the server</param>
        /// <param name="user">User who connects to the Cognigy AI</param>
        /// <param name="apikey">Apikey needed for the verification</param>
        /// <param name="channel"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<string> GetToken
        (
            string baseUrl,
            string user,
            string apikey,
            string channel,
            string token
        )
        {
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
            else
            {
                return await Fetch(baseUrl, user, apikey, channel);
            }
        }

        private async Task<string> Fetch
        (
            string baseUrl,
            string user,
            string apikey,
            string channel
        )
        {
            CognigyFetchRequest cognigyFetchRequest = new CognigyFetchRequest(baseUrl, user, apikey, channel);
            return await cognigyFetchRequest.GetToken();
        }

        private async Task<Socket> EstablishSocketConnection(string token)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>()
            {
                {"token", token},
                {"upgrade", "false"}
            };

            var options = new IO.Options()
            {
                Reconnection = true,
                AutoConnect = true,
                Query = queryParams,
                Upgrade = false

            };

            this.mySocket = IO.Socket(this.options.baseUrl, options);

            this.mySocket.On("connect", () =>
            {
                waitHandle.Set();
                this.connected = true;
            });

            this.mySocket.On("connecting", data => LogStatus("CONNECTION", "Connecting to the server"));
            this.mySocket.On("connect_error", data => LogError("CONNECTION ERROR", Convert.ToString(data)));
            this.mySocket.On("connect_timeout", data => LogError("CONNECTION TIMEOUT", Convert.ToString(data)));
            this.mySocket.On("error", data => LogError("ERROR", Convert.ToString(data)));
            this.mySocket.On("exception", data => LogStatus("EXCEPTON", Convert.ToString(data)));
            this.mySocket.On("output", (data) => { OnOutput?.Invoke(this, new OutputEventArgs(DeserializeToOutput(Convert.ToString(data)))); });
            //this.mySocket.On("logStep", (data) => { if (OnOutput != null) OnOutput(this, new OutputEventArgs(DeserializeToOutput(Convert.ToString(data)))); }));
            //this.mySocket.On("logStepError", (data) => OnOutput(Convert.ToString(data)));

            waitHandle.WaitOne();
            return this.mySocket;
        }

        private async Task InitializeCognigyClient(Socket socket)
        {
            bool? resetState = false;
            bool? resetContext = false;

            if (this.options.resetState != null && this.options.resetState == true)
                resetState = true;

            if (this.options.resetContext != null && this.options.resetContext == true)
                resetContext = true;

            InitializationParameters initParam = new InitializationParameters(
                this.options.flow,
                this.options.language,
                this.options.version,
                this.options.passthroughIP,
                resetState,
                resetContext
                );

            socket.Emit("init", JObject.FromObject(initParam));

            socket.On("initResponse", () => waitHandle.Set());
            socket.On("exception", data => LogStatus("EXCEPTON", "Error in brain initialization"));

            waitHandle.WaitOne();
            return;
        }

        /// <summary>
        /// Disconnects the client from the Cognigy AI
        /// </summary>
        public void Disconnect()
        {
            if (this.mySocket != null)
                this.mySocket.Disconnect();
        }

        public bool IsConnected()
        {
            if (this.mySocket != null && this.connected)
                return true;
            else
                return false;
        }

        public void SendMessage<T>(string text, T data)
        {
            if (this.IsConnected())
            {
                Message<T> message = new Message<T>(text, data);
                this.mySocket.Emit("input", JObject.FromObject(message));
            }
            else
            {
                LogError("SENDMESSAGE ERROR", "we are not connected");
            }
        }

        public void SendMessage(string text)
        {
            if (this.IsConnected())
            {
                Message<object> message = new Message<object>(text, null);
                this.mySocket.Emit("input", JObject.FromObject(message));
            }
            else
            {
                LogError("SENDMESSAGE ERROR", "we are not connected");
            }
        }
    }
}
