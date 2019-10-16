using Microsoft.Analytics.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace DecisionServiceExtractor
{
    internal class CbParser
    {
        private readonly bool hasEventId;
        private readonly bool hasTimestamp;
        private readonly bool hasEnqueuedTimeUtc;
        private readonly bool hasCost;
        private readonly bool hasProbability;
        private readonly bool hasAction;
        private readonly bool hasNumActions;
        private readonly bool hasHasObservations;
        private readonly bool hasData;
        private readonly bool hasJsonObject;
        private readonly bool hasPdrop;
        private readonly bool hasIsDangling;
        private readonly bool hasSkipLearn;
        private readonly bool hasSessionId;

        private readonly int idxEventId;
        private readonly int idxTimestamp;
        private readonly int idxEnqueuedTimeUtc;
        private readonly int idxCost;
        private readonly int idxProbability;
        private readonly int idxAction;
        private readonly int idxNumActions;
        private readonly int idxHasObservations;
        private readonly int idxData;
        private readonly int idxPdrop;
        private readonly int idxIsDangling;
        private readonly int idxSkipLearn;
        private readonly int idxSessionId;

        private FieldExpression[] expressions;

        internal CbParser(ISchema schema, FieldExpression[] expressions)
        {
            this.expressions = expressions;

            this.hasEventId = Helpers.HasColumnOfType(schema, "EventId", typeof(string), out this.idxEventId);
            this.hasTimestamp = Helpers.HasColumnOfType(schema, "Timestamp", typeof(DateTime), out this.idxTimestamp);
            this.hasEnqueuedTimeUtc = Helpers.HasColumnOfType(schema, "EnqueuedTimeUtc", typeof(DateTime), out this.idxEnqueuedTimeUtc);
            this.hasCost = Helpers.HasColumnOfType(schema, "Cost", typeof(float), out this.idxCost);
            this.hasProbability = Helpers.HasColumnOfType(schema, "Prob", typeof(float), out this.idxProbability);
            this.hasAction = Helpers.HasColumnOfType(schema, "Action", typeof(int), out this.idxAction);
            this.hasNumActions = Helpers.HasColumnOfType(schema, "NumActions", typeof(int), out this.idxNumActions);
            this.hasHasObservations = Helpers.HasColumnOfType(schema, "HasObservations", typeof(int), out this.idxHasObservations);
            this.hasData = Helpers.HasColumnOfType(schema, "Data", typeof(string), out this.idxData);
            this.hasPdrop = Helpers.HasColumnOfType(schema, "pdrop", typeof(float), out this.idxPdrop);
            this.hasIsDangling = Helpers.HasColumnOfType(schema, "IsDangling", typeof(bool), out this.idxIsDangling);
            this.hasSkipLearn = Helpers.HasColumnOfType(schema, "SkipLearn", typeof(bool), out this.idxSkipLearn);

            this.hasSessionId = Helpers.HasColumnOfType(schema, "SessionId", typeof(string), out this.idxSessionId);

            this.hasJsonObject = false;
            foreach (var fe in expressions)
            {
                fe.Idx = -1;
                this.hasJsonObject |= Helpers.HasColumnOfType(schema, fe.FieldName, typeof(string), out int idx);
                fe.Idx = idx;
            }
        }

        //called on every line
        public IRow ParseEvent(IUpdatableRow output, Stream input)
        {
            TextReader inputReader;
            if (this.hasSkipLearn)
            {
                output.Set(this.idxSkipLearn, false);
            }
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

                if (this.hasEnqueuedTimeUtc)
                    output.Set(this.idxEnqueuedTimeUtc, jobj.Value<DateTime>("EnqueuedTimeUtc"));

                if (this.hasSkipLearn)
                {
                    var optional = jobj.Value<bool?>("_skipLearn"); ;
                    output.Set(this.idxSkipLearn, (bool)(optional.HasValue ? optional : false));
                }

                if (this.hasCost)
                    output.Set(this.idxCost, (float)jobj.Value<double>("_label_cost"));

                if (this.hasProbability)
                    output.Set(this.idxProbability, (float)jobj.Value<double>("_label_probability"));

                if (this.hasAction)
                    output.Set(this.idxAction, (int)jobj.Value<long>("_label_Action"));

                if (this.hasPdrop)
                {
                    var optional = jobj.Value<double?>("pdrop");
                    output.Set(this.idxPdrop, (float)(optional.HasValue ? optional.Value : 0.0f));
                }

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
                                        Helpers.ExtractPropertyString(jsonReader, output, this.idxEventId, this.hasEventId);
                                        break;
                                    case "Timestamp":
                                        if (this.hasTimestamp)
                                            output.Set(this.idxTimestamp, (DateTime)jsonReader.ReadAsDateTime());
                                        break;
                                    case "_label_cost":
                                        foundLabelCost = true;
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.idxCost, this.hasCost);
                                        if (this.hasIsDangling)
                                            output.Set(this.idxIsDangling, false);
                                        break;
                                    case "_label_probability":
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.idxProbability, this.hasProbability);
                                        break;
                                    case "_label_Action":
                                        Helpers.ExtractPropertyInteger(jsonReader, output, this.idxAction, this.hasAction);
                                        break;
                                    case "a":
                                        if (!this.hasNumActions)
                                            jsonReader.Skip();
                                        else
                                            output.Set(this.idxNumActions, Helpers.CountArrayElements(jsonReader));
                                        break;
                                    case "o":
                                        if (!this.hasHasObservations)
                                            jsonReader.Skip();
                                        else
                                            output.Set(this.idxHasObservations, 1);
                                        break;
                                    case "pdrop":
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.idxPdrop, this.hasPdrop);
                                        break;
                                    case "_skipLearn":
                                        Helpers.ExtractPropertyBool(jsonReader, output, this.idxSkipLearn, this.hasSkipLearn);
                                        break;
                                    case "EnqueuedTimeUtc":
                                        if (this.hasEnqueuedTimeUtc)
                                            output.Set(this.idxEnqueuedTimeUtc, (DateTime)jsonReader.ReadAsDateTime());
                                        if (this.hasIsDangling)
                                            output.Set(this.idxIsDangling, true);
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

            return output.AsReadOnly();
        }
    }

}
