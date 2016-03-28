using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
    /// If no <see cref="JsonPropertyAttribute"/> is specified, the C# type name is used
    /// as property name.
    /// </summary>
    public class DecisionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
                return;

            writer.WriteStartObject();

            var type = value.GetType();

            foreach (var property in type.GetProperties())
            {
                var jsonProp = (JsonPropertyAttribute)property.GetCustomAttributes(typeof(JsonPropertyAttribute), true).FirstOrDefault();

                string propName;
                if (jsonProp != null && jsonProp.PropertyName != null)
                {
                    propName = jsonProp.PropertyName;
                }
                else
                {
                    propName = property.PropertyType.Name;
                }

                writer.WritePropertyName(propName);
                serializer.Serialize(writer, property.GetValue(value));
            }

            writer.WriteEndObject();
        }
    }
}
