using System;

namespace Cognigy
{
    [Serializable]
    class Message<T> : Message
    {
        public T data;
    }

    [Serializable]
    class Message
    {
        public string URLToken;

        public string userId;

        public string sessionId;

        public string source;

        public string passthroughIP;

        public bool reloadFlow;

        public bool resetFlow;

        public bool resetState;

        public bool resetContext;

        public string text;
    }
}
