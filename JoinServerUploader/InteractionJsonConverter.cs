using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    public class InteractionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Interaction);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var v = value as Interaction;
            if (v == null)
                return;

            writer.WriteStartObject();

            writer.WritePropertyName("EventId");
            writer.WriteValue(v.Key);

            writer.WritePropertyName("TimeStamp");
            writer.WriteValue(v.TimeStamp);

            if (v.Value != null)
            {
                writer.WritePropertyName("a");
                serializer.Serialize(writer, v.Value);
            }

            if (v.Context != null)
            {
                writer.WritePropertyName("c");
                var contextString = v.Context as string;
                if (contextString != null)
                    writer.WriteRawValue(contextString);
                else
                    serializer.Serialize(writer, v.Context);
            }

            if (v.ExplorerState != null)
            {
                // TODO: look at this
                var exploreType = v.ExplorerState.GetType();
                var jsonObjectAnnotation = (JsonObjectAttribute)exploreType
                    .GetCustomAttributes(typeof(JsonObjectAttribute), inherit: false)
                    .FirstOrDefault();

                if (jsonObjectAnnotation == null)
	            {
                    throw new NotSupportedException(exploreType.Name + " is not annotated with JsonObject.");
	            }

                writer.WritePropertyName(jsonObjectAnnotation.Id);
                serializer.Serialize(writer, v.ExplorerState);
            }

            if (v.MapperState != null)
            {
                writer.WritePropertyName(v.MapperState.GetType().Name);
                serializer.Serialize(writer, v.MapperState);
            }

            if (v.ProbabilityOfDrop != null)
            {
                writer.WritePropertyName("p"); // TODO: change to pdrop?
                serializer.Serialize(writer, v.ProbabilityOfDrop);
            }

            writer.WriteEndObject();
        }
    }
}
