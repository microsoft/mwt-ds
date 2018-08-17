using Microsoft.Analytics.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DecisionServiceExtractor
{
    [SqlUserDefinedExtractor(AtomicFileProcessing = false)]
    public class HeaderOnly : IExtractor
    {
        private class InternalParser
        {
            private static void ExtractPropertyString(JsonTextReader jsonReader, IUpdatableRow output, int fieldIdx, bool hasField)
            {
                jsonReader.Read();
                if (hasField)
                    output.Set(fieldIdx, (string)jsonReader.Value);
            }

            private static void ExtractPropertyInteger(JsonTextReader jsonReader, IUpdatableRow output, int fieldIdx, bool hasField)
            {
                jsonReader.Read();
                if (hasField)
                    output.Set(fieldIdx, (int)(long)jsonReader.Value);
            }

            private static void ExtractPropertyDouble(JsonTextReader jsonReader, IUpdatableRow output, int fieldIdx, bool hasField)
            {
                jsonReader.Read();

                if (hasField)
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.Integer:
                            output.Set(fieldIdx, (float)(long)jsonReader.Value);
                            break;
                        case JsonToken.Float:
                            output.Set(fieldIdx, (float)(double)jsonReader.Value);
                            break;
                        default:
                            throw new Exception("wrong data type");
                    }
                }
            }

            private static bool HasColumnOfType(ISchema schema, string name, Type t, out int idx)
            {
                idx = schema.IndexOf(name);
                if (idx < 0)
                    return false;

                var column = schema[idx];

                // make sure we can write
                if (column.IsReadOnly)
                    return false;

                // make sure it's the proper type
                return column.Type == t;
            }

            private readonly bool hasEventId;
            private readonly bool hasTimestamp;
            private readonly bool hasCost;
            private readonly bool hasProbability;
            private readonly bool hasAction;
            private readonly bool hasNumActions;
            private readonly bool hasHasObservations;
            private readonly bool hasData;
            private readonly bool hasJsonObject;

            private readonly int idxEventId;
            private readonly int idxTimestamp;
            private readonly int idxCost;
            private readonly int idxProbability;
            private readonly int idxAction;
            private readonly int idxNumActions;
            private readonly int idxHasObservations;
            private readonly int idxData;
            private FieldExpression[] expressions;

            internal InternalParser(ISchema schema, FieldExpression[] expressions)
            {
                this.expressions = expressions;

                this.hasEventId = HasColumnOfType(schema, "EventId", typeof(string), out this.idxEventId);
                this.hasTimestamp = HasColumnOfType(schema, "Timestamp", typeof(DateTime), out this.idxTimestamp);
                this.hasCost = HasColumnOfType(schema, "Cost", typeof(float), out this.idxCost);
                this.hasProbability = HasColumnOfType(schema, "Prob", typeof(float), out this.idxProbability);
                this.hasAction = HasColumnOfType(schema, "Action", typeof(int), out this.idxAction);
                this.hasNumActions = HasColumnOfType(schema, "NumActions", typeof(int), out this.idxNumActions);
                this.hasHasObservations = HasColumnOfType(schema, "HasObservations", typeof(int), out this.idxHasObservations);
                this.hasData = HasColumnOfType(schema, "Data", typeof(string), out this.idxData);

                this.hasJsonObject = false;
                foreach (var fe in expressions)
                {
                    fe.Idx = -1;
                    this.hasJsonObject |= HasColumnOfType(schema, fe.FieldName, typeof(string), out int idx);
                    fe.Idx = idx;
                }
            }

            private static int CountArrayElements(JsonTextReader jsonReader)
            {
                int numActions = 0;

                while (jsonReader.Read())
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.Integer:
                            numActions++;
                            break;
                        case JsonToken.EndArray:
                            return numActions;
                    }
                }

                return numActions;
            }

            public IRow ParseEvent(IUpdatableRow output, Stream input)
            {
                TextReader inputReader;

                if (!this.hasData)
                    inputReader = new StreamReader(input, Encoding.UTF8);
                else
                {
                    string data = new StreamReader(input, Encoding.UTF8).ReadToEnd();
                    inputReader = new StringReader(data);

                    if (this.hasData)
                        output.Set(this.idxData, data);
                }

                if (this.hasJsonObject)
                {
                    var jsonReader = new JsonTextReader(inputReader);
                    jsonReader.DateFormatString = "o";
                    var jobj = JObject.ReadFrom(jsonReader);

                    var chosenActionIndex = (int)jobj.Value<long>("_labelIndex");

                    // iterate through all expressions
                    foreach (var fe in this.expressions)
                    {
                        if (fe.Idx >= 0)
                        {
                            // special support for chosen action
                            var expr = fe.JsonPath.Replace("$.c._multi[($._labelIndex)]", $"$.c._multi[{chosenActionIndex}]");
                            try
                            {
                                string value = jobj.SelectToken(expr)?.Value<string>();
                                if (value != null)
                                    output.Set(fe.Idx, value);
                            }
                            catch (Exception e)
                            {
                                output.Set(fe.Idx, e.Message);
                            }
                        }
                    }

                    // since we already parse it, no need to parse twice
                    if (this.hasEventId)
                        output.Set(this.idxEventId, jobj.Value<string>("EventId"));

                    if (this.hasTimestamp)
                        output.Set(this.idxTimestamp, jobj.Value<DateTime>("Timestamp"));

                    if (this.hasCost)
                        output.Set(this.idxCost, (float)jobj.Value<double>("_label_cost"));

                    if (this.hasProbability)
                        output.Set(this.idxProbability, (float)jobj.Value<double>("_label_probability"));

                    if (this.hasAction)
                        output.Set(this.idxAction, (int)jobj.Value<long>("_label_Action"));

                    if (this.hasNumActions)
                    {
                        if (jobj["a"] is JArray arr)
                            output.Set(this.idxNumActions, arr.Count);
                    }

                    if (this.hasHasObservations)
                    {
                        if (jobj["o"] is JArray arr)
                            output.Set(this.idxHasObservations, 1);
                    }

                    // return early
                    return output.AsReadOnly();
                }
                // TODO: skip the dangling events
                bool foundLabelCost = false;

                // this is a optimized version only parsing parts of the data
                using (var jsonReader = new JsonTextReader(inputReader))
                {
                    jsonReader.DateFormatString = "o";
                    while (jsonReader.Read())
                    {
                        switch (jsonReader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                {
                                    var propertyName = (string)jsonReader.Value;

                                    // "_label_cost":0,"_label_probability":0.200000003,"_label_Action":4,"_labelIndex":3,"Timestamp":"2018-03-30T01:48:11.5760000Z","Version":"1","EventId":
                                    switch (propertyName)
                                    {
                                        case "EventId":
                                            ExtractPropertyString(jsonReader, output, this.idxEventId, this.hasEventId);
                                            break;
                                        case "Timestamp":
                                            if (this.hasTimestamp)
                                                output.Set(this.idxTimestamp, (DateTime)jsonReader.ReadAsDateTime());
                                            break;
                                        case "_label_cost":
                                            foundLabelCost = true;
                                            ExtractPropertyDouble(jsonReader, output, this.idxCost, this.hasCost);
                                            break;
                                        case "_label_probability":
                                            ExtractPropertyDouble(jsonReader, output, this.idxProbability, this.hasProbability);
                                            break;
                                        case "_label_Action":
                                            ExtractPropertyInteger(jsonReader, output, this.idxAction, this.hasAction);
                                            break;
                                        case "a":
                                            if (!this.hasNumActions)
                                                jsonReader.Skip();
                                            else
                                                output.Set(this.idxNumActions, CountArrayElements(jsonReader));
                                            break;
                                        case "o":
                                            if (!this.hasHasObservations)
                                                jsonReader.Skip();
                                            else
                                                output.Set(this.idxHasObservations, 1);
                                            break;
                                        default:
                                            jsonReader.Skip();
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                }

                // skip dangling events
                return foundLabelCost ? output.AsReadOnly() : null;
            }
        }

        class FieldExpression
        {
            public string FieldName;

            public string JsonPath;

            public int Idx;
        }

        private readonly FieldExpression[] expressions;

        public HeaderOnly(params string[] expressions)
        {
            // TODO: error handling
            this.expressions = expressions
                .Select(l =>
                {
                    var index = l.IndexOf(' ');
                    return new FieldExpression { FieldName = l.Substring(0, index), JsonPath = l.Substring(index + 1) };
                })
                .ToArray();
        }


        public override IEnumerable<IRow> Extract(IUnstructuredReader input, IUpdatableRow output)
        {
            var parser = new InternalParser(output.Schema, this.expressions);

            foreach (Stream current in input.Split((byte)'\n'))
            {
                var row = parser.ParseEvent(output, current);
                if (row != null)
                    yield return row;
            }
        }
    }
}
