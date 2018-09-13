using System;

namespace Cognigy
{
    public class Options
    {
        public string endpointURL;
        public string URLToken;
        public string userId;
        public string sessionId;
        public string channel;

        public bool? keepMarkup;

        public bool? reconnection;
        public int? interval;
        public long? expiresIn;

        public bool? resetState;
        public bool? resetContext;
        public bool? reloadFlow;
        public bool? resetFlow;

        public Action<ErrorResponse> handleError;
        public Action<ErrorResponse> handleException;
        public Action<FlowResponse> handleOutput;

        public Action<FinalPing> handlePing;

        public string passthroughIP;
    }
}
