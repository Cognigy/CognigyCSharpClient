using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.EngineIoClientDotNet.Client.Transports;
using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Cognigy
{
    public class CognigyClient
    {
        private const string EVENT_PROCESS_INPUT = "processInput";
        private const string EVENT_OUTPUT = "output";
        private const string EVENT_EXCEPTION = "exception";
        private const string EVENT_FINAL_PING = "finalPing";

        private Options options;
        private Socket socketClient;
        private long lastUsed;

        private bool connected = false;

        private Action<string, string> LogError = (type, message) => Console.Error.WriteLine(string.Format("[{0}] {1}", type, message));
        private Action<string, string> LogStatus = (status, message) => Console.WriteLine(string.Format("[{0}] {1}", status, message));

        private static AutoResetEvent waitHandle = new AutoResetEvent(false);

        public CognigyClient(Options options)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;

            this.options = options;

            if (this.options.keepMarkup == null)
                this.options.keepMarkup = false;

            this.UpdateLastUsed();
        }

        /// <summary>
        /// Connect the CognigyClient to the Socket Endpoint on COGNIGY.AI
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            ImmutableList<string> transports = ImmutableList.Create<string>(WebSocket.NAME, Polling.NAME);

            IO.Options options = new IO.Options()
            {
                ForceNew = true,
                Reconnection = false,
                AutoConnect = true,
                Upgrade = true,
                Secure = true,
                Multiplex = false,
                Transports = transports
            };

            this.socketClient = IO.Socket(new Uri(this.options.endpointURL), options);

            this.socketClient.On(Socket.EVENT_CONNECT, () =>
            {
                waitHandle.Set();
                this.connected = true;
            });

            this.socketClient.On(Socket.EVENT_CONNECT_ERROR, data => LogError("CONNECTION ERROR", "Error connecting"));
            this.socketClient.On(Socket.EVENT_CONNECT_TIMEOUT, data => LogError("CONNECTION TIMEOUT", "Error connecting"));

            this.socketClient.On(Socket.EVENT_ERROR, data =>
            {
                JObject jObject = JObject.Parse(Convert.ToString(data));
                ErrorResponse errorResponse = jObject["data"]["error"].ToObject<ErrorResponse>();

                if (this.options.handleError != null)
                    this.options.handleError(errorResponse);

                LogError("ERROR", errorResponse.message);
            });

            this.socketClient.On(EVENT_EXCEPTION, data =>
            {
                JObject jObject = JObject.Parse(Convert.ToString(data));
                ErrorResponse errorResponse = jObject["data"]["error"].ToObject<ErrorResponse>();

                if (this.options.handleError != null)
                    this.options.handleError(errorResponse);

                LogError("EXCEPTION", errorResponse.message);
            });

            // Handling outputs from AI
            this.socketClient.On(EVENT_OUTPUT, (data) =>
            {
                string response = Convert.ToString(data);
                AIOutput aiOutput = JsonConvert.DeserializeObject<AIOutput>(response);

                switch (aiOutput.type)
                {
                    case OutputType.error:
                        ErrorResponse errorResponse = aiOutput.data["error"].ToObject<ErrorResponse>();

                        if (this.options.handleError != null)
                            this.options.handleError(errorResponse);

                        LogError("ERROR", errorResponse.message);
                        break;

                    case OutputType.output:
                        FlowResponse flowResponse = aiOutput.data.ToObject<FlowResponse>();

                        if (this.options.handleOutput != null)
                            this.options.handleOutput(flowResponse);

                        LogStatus("OUTPUT", JsonConvert.SerializeObject(flowResponse));
                        break;
                }
            });

            this.socketClient.On(EVENT_FINAL_PING, (data) =>
            {
                string response = Convert.ToString(data);
                FinalPing finalPing = JsonConvert.DeserializeObject<FinalPing>(response);

                if (this.options.handlePing != null)
                    this.options.handlePing(finalPing);

                LogStatus("FINAL_PING", response);
            });

            // Blocking the thread until we receive the connect event
            waitHandle.WaitOne();
        }

        /// <summary>
        /// Disconnects the client from the Cognigy AI
        /// </summary>
        public void Disconnect()
        {
            if (this.socketClient != null)
                this.socketClient.Disconnect();
        }

        /// <summary>
        /// Checks whether the client has a socket and whether it is connected to COGNIGY.AI
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            return this.socketClient != null && this.connected;
        }

        /// <summary>
        /// Checks whether this client is already expired. Used to express whether the
	    /// client wasn't used for a long time.
        /// </summary>
        public bool IsExpired()
        {
            if (this.options.expiresIn == null)
                return false;

            return (DateTime.Now.Ticks - this.lastUsed) > this.options.expiresIn;
        }

        /// <summary>
        /// Sends a message to a COGNIGY.AI socket endpoint
        /// </summary>
        /// <param name="text">The input text</param>
        /// <param name="data">The input data</param>
        public void SendMessage<T>(string text, T data)
        {
            if (this.IsConnected())
            {
                this.UpdateLastUsed();

                Message<T> message = new Message<T>
                {
                    URLToken = this.options.URLToken,
                    userId = this.options.userId,
                    sessionId = this.options.sessionId,
                    source = "device",
                    passthroughIP = this.options.passthroughIP,
                    reloadFlow = this.options.reloadFlow ?? false,
                    resetFlow = this.options.resetFlow ?? false,
                    resetState = this.options.resetState ?? false,
                    resetContext = this.options.resetContext ?? false,
                    text = text,
                    data = data
                };

                this.socketClient.Emit(EVENT_PROCESS_INPUT, JObject.FromObject(message));
            }
            else
            {
                LogError("SENDMESSAGE", "client not connected");
            }
        }

        /// <summary>
        /// Send a simple text message
        /// </summary>
        /// <param name="text">The message text</param>
        public void SendMessage(string text)
        {
            if (this.IsConnected())
            {
                Message message = new Message
                {
                    URLToken = this.options.URLToken,
                    userId = this.options.userId,
                    sessionId = this.options.sessionId,
                    source = "device",
                    passthroughIP = this.options.passthroughIP,
                    reloadFlow = this.options.reloadFlow ?? false,
                    resetFlow = this.options.resetFlow ?? false,
                    resetState = this.options.resetState ?? false,
                    resetContext = this.options.resetContext ?? false,
                    text = text
                };

                this.socketClient.Emit(EVENT_PROCESS_INPUT, JObject.FromObject(message));
            }
            else
            {
                LogError("SENDMESSAGE", "client not connected");
            }
        }

        /// <summary>
        /// Updates the last-used timestamp
        /// </summary>
        private void UpdateLastUsed()
        {
            this.lastUsed = DateTime.Now.Ticks - new DateTime(1970, 1, 1).Ticks;
        }
    }
}
