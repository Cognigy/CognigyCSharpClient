using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Cognigy
{
    class CognigyFetchRequest
    {
        private const string LOGIN_DEVICE_ADDR = "/loginDevice";
        private const string JSON_MEDIA_TYPE = "application/json";

        private MediaTypeWithQualityHeaderValue mediaType = new MediaTypeWithQualityHeaderValue(JSON_MEDIA_TYPE);

        private RequestBodyContent requestBodyContent;
        private HttpClient httpClient;
        private HttpRequestMessage requestMessage;

        private Action<string, string> LogError = (type, message) => Console.Error.WriteLine(string.Format("-- {0} -- \n{1} \n", type, message));

        private Uri baseAddress;

        public CognigyFetchRequest
        (
            string baseURL,
            string user,
            string apiKey,
            string channel
        )
        {
            this.baseAddress = new Uri(baseURL + LOGIN_DEVICE_ADDR);

            requestBodyContent = new RequestBodyContent(user, apiKey, channel);

            ConfigureCognigyRequest();
        }

        public async Task<string> GetToken()
        {
            HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage);

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
                    return responseContent.token;
            }
            else
            {
                LogError("REQUEST ERROR", "Status Code: " + responseMessage.StatusCode);
                return null;
            }
        }

        private void ConfigureCognigyRequest()
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = baseAddress;
            httpClient.DefaultRequestHeaders.Accept.Add(mediaType);

            requestMessage = new HttpRequestMessage(HttpMethod.Post, baseAddress);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestBodyContent), Encoding.UTF8, JSON_MEDIA_TYPE);
        }
    }
}
