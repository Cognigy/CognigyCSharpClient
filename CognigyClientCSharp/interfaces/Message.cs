using Newtonsoft.Json;
using System;

namespace Cognigy
{
    [Serializable]
    class Message<T>
    {
        public string text;

        public T data;

        public Message(string text, T data)
        {
            this.text = text;
            this.data = data;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
