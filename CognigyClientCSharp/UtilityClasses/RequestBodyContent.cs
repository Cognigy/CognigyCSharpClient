using Newtonsoft.Json;
using System;

namespace Cognigy
{
    [Serializable]
    class RequestBodyContent
    {
        [JsonProperty]
        public string user { get; private set; }

        [JsonProperty]
        public string apikey { get; private set; }

        [JsonProperty]
        public string channel { get; private set; }

        public RequestBodyContent(string user, string apikey, string channel)
        {
            this.user = user;
            this.apikey = apikey;
            this.channel = channel;

        }
    }
}
