using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Cognigy
{
    public enum OutputType
    {
        output,
        error
    }

    struct AIOutput
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OutputType type;
        public JContainer data;
    }
}
