using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cognigy
{
    class CognigyClient
    {
        private Options options;
        private Socket mySocket;
        private bool firstLoad;

        private bool connected = false;

        Action<string, string> LogError = (type, message) => Console.Error.WriteLine(string.Format("-- {0} -- \n{1} \n", type, message));
        Action<string, string> LogStatus = (status, message) => Console.WriteLine(string.Format("--{0}: {1} --\n", status, message));

        Func<string, Output> onOutput = (output) => JsonConvert.DeserializeObject<Output>(output);

        public delegate void onResponseEvent(Output output);
        public event onResponseEvent OnResponse;

        private static AutoResetEvent waitHandle = new AutoResetEvent(false);

        public CognigyClient(Options options)
        {
            this.options = options;
            this.firstLoad = true;
        }

        #region Cognigy Client Initialization
        public async Task Connect()
        {
            string token = await GetToken(this.options.baseUrl, this.options.user, this.options.apikey, this.options.channel, this.options.token);
            Socket socket = await EstablishSocketConnection(token);
            await InitializeCognigyClient(socket);
            Console.WriteLine("We are connected");
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
        private async Task<string> GetToken(string baseUrl, string user, string apikey, string channel, string token)
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

        private async Task<string> Fetch(string baseUrl, string user, string apikey, string channel)
        {
            string jsonString = JsonConvert.SerializeObject(new RequestBodyContent(user, apikey, channel));

            HttpClient requestClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl + "/loginDevice"),
            };

            requestClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUrl + "/loginDevice"))
            {
                Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage responseMessage = await requestClient.SendAsync(requestMessage);
            if (responseMessage.IsSuccessStatusCode)
            {
                string rawResponseContent = await responseMessage.Content.ReadAsStringAsync();
                ResponseBodyContent responseContent = JsonConvert.DeserializeObject<ResponseBodyContent>(rawResponseContent);

                if (string.IsNullOrEmpty(responseContent.token))
                {
                    LogError("REQUEST ERROR", "No token received");
                    return null;
                }
                else
                {
                    return responseContent.token;
                }
            }
            else
            {
                LogError("REQUEST ERROR", "Status Code: " + responseMessage.StatusCode);
                return null;
            }
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
            this.mySocket.On("output", (data) => OnResponse(onOutput(Convert.ToString(data))));
            this.mySocket.On("logStep", (data) => onOutput(Convert.ToString(data)));
            this.mySocket.On("logStepError", (data) => onOutput(Convert.ToString(data)));

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
        #endregion

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

        /// <summary>
        /// Resets the flow to another flow or to default
        /// </summary>
        /// <param name="newFlowId">Flow to reset to</param>
        /// <param name="language">Language of the flow</param>
        /// <param name="version">Version of the flow</param>
        public void ResetFlow(string newFlowId, string language, float? version = null)
        {
            if (this.IsConnected())
            {
                Dictionary<string, object> resetFlowParam = new Dictionary<string, object>()
            {
                {"id", newFlowId},
                {"language", language},
                {"version", version}
            };
                //logStatus("RESET FLOW", JsonConvert.SerializeObject(resetFlowParam));
                this.mySocket.Emit("resetFlow", JObject.FromObject(resetFlowParam));
            }
            else
                LogError("RESETFLOW ERROR", "we are not connected");
        }

        /// <summary>
        /// Resets the state of the flow to default
        /// </summary>
        public void RestState()
        {
            if (this.IsConnected())
                this.mySocket.Emit("resetState");
            else
                LogError("RESETSTATE ERROR", "we are not connected");
        }

        /// <summary>
        /// Resets the context of the flow to default
        /// </summary>
        public void ResetContext()
        {
            if (this.IsConnected())
                this.mySocket.Emit("resetContext");
            else
                LogError("RESETCONTEXT ERROR", "we are not connected");
        }

        public async Task<string> InjectContext<T>(T context)
        {
            if (this.IsConnected())
            {
                string newContext = null;
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                this.mySocket.Emit("injectContext", (callback) =>
                {
                    newContext = Convert.ToString(callback);
                    manualResetEvent.Set();
                }, JObject.FromObject(context));

                manualResetEvent.WaitOne();
                return newContext;
            }
            else
            {
                LogError("INJECTCONTEXT ERROR", "we are not connected");
                return null;
            }
        }

        public async Task<string> InjectState(string state)
        {
            if (this.IsConnected())
            {
                string newState = null;
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);

                this.mySocket.Emit("injectState", (callback) =>
                {
                    newState = Convert.ToString(callback);
                    manualResetEvent.Set();

                }, state);

                manualResetEvent.WaitOne();
                return newState;
            }
            else
            {
                LogError("INJECTSTATE ERROR", "we are not connected");
                return null;
            }
        }
    }
}
