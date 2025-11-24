using System.Text.Json.Serialization;

namespace DmpStack;

[JsonSerializable(typeof(Frame[]))]
public partial class FramesJsonSerializationContext : JsonSerializerContext
{
}
