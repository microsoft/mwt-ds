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
        private readonly ColumnInfo EventIdColumn;
        private readonly ColumnInfo TimestampColumn;
        private readonly ColumnInfo EnqueuedTimeUtcColumn;
        private readonly ColumnInfo CostColumn;
        private readonly ColumnInfo ProbabilityColumn;
        private readonly ColumnInfo ActionColumn;
        private readonly ColumnInfo NumActionsColumn;
        private readonly ColumnInfo HasObservationsColumn;
        private readonly ColumnInfo DataColumn;
        private readonly ColumnInfo PdropColumn;
        private readonly ColumnInfo IsDanglingColumn;
        private readonly ColumnInfo SkipLearnColumn;
        private readonly ColumnInfo RewardValueColumn;

        private readonly bool hasJsonObject;
        private FieldExpression[] expressions;

        internal CbParser(ISchema schema, FieldExpression[] expressions)
        {
            this.expressions = expressions;

            this.EventIdColumn = new ColumnInfo(schema, "EventId", typeof(string));
            this.TimestampColumn = new ColumnInfo(schema, "Timestamp", typeof(DateTime));
            this.EnqueuedTimeUtcColumn = new ColumnInfo(schema, "EnqueuedTimeUtc", typeof(DateTime));
            this.CostColumn = new ColumnInfo(schema, "Cost", typeof(float));
            this.ProbabilityColumn = new ColumnInfo(schema, "Prob", typeof(float));
            this.ActionColumn = new ColumnInfo(schema, "Action", typeof(int));
            this.NumActionsColumn = new ColumnInfo(schema, "NumActions", typeof(int));
            this.HasObservationsColumn = new ColumnInfo(schema, "HasObservations", typeof(int));
            this.DataColumn = new ColumnInfo(schema, "Data", typeof(string));
            this.PdropColumn = new ColumnInfo(schema, "pdrop", typeof(float));
            this.IsDanglingColumn = new ColumnInfo(schema, "IsDangling", typeof(bool));
            this.SkipLearnColumn = new ColumnInfo(schema, "SkipLearn", typeof(bool));
            this.RewardValueColumn = new ColumnInfo(schema, "RewardValue", typeof(float?));

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
            output.Set(this.SkipLearnColumn, false);
            if (!this.DataColumn.IsRequired)
                inputReader = new StreamReader(input, Encoding.UTF8);
            else
            {
                string data = new StreamReader(input, Encoding.UTF8).ReadToEnd();
                inputReader = new StringReader(data);
                output.Set(this.DataColumn, data);
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
                output.Set(this.EventIdColumn, jobj.Value<string>("EventId"));
                output.Set(this.TimestampColumn, jobj.Value<DateTime>("Timestamp"));
                output.Set(this.EnqueuedTimeUtcColumn, jobj.Value<DateTime>("EnqueuedTimeUtc"));

                if (this.SkipLearnColumn.IsRequired)
                {
                    var optional = jobj.Value<bool?>("_skipLearn"); ;
                    output.Set(this.SkipLearnColumn.Idx, (bool)(optional.HasValue ? optional : false));
                }

                output.Set(this.CostColumn, (float)jobj.Value<double>("_label_cost"));
                output.Set(this.ProbabilityColumn, (float)jobj.Value<double>("_label_probability"));
                output.Set(this.ActionColumn, (int)jobj.Value<long>("_label_Action"));

                if (this.PdropColumn.IsRequired)
                {
                    var optional = jobj.Value<double?>("pdrop");
                    output.Set(this.PdropColumn.Idx, (float)(optional.HasValue ? optional.Value : 0.0f));
                }

                if (this.NumActionsColumn.IsRequired)
                {
                    if (jobj["a"] is JArray arr)
                        output.Set(this.NumActionsColumn.Idx, arr.Count);
                }

                if (this.HasObservationsColumn.IsRequired)
                {
                    if (jobj["o"] is JArray arr)
                        output.Set(this.HasObservationsColumn.Idx, 1);
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
                                        Helpers.ExtractPropertyString(jsonReader, output, this.EventIdColumn);
                                        break;
                                    case "Timestamp":
                                        output.Set(this.TimestampColumn, (DateTime)jsonReader.ReadAsDateTime());
                                        break;
                                    case "_label_cost":
                                        foundLabelCost = true;
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.CostColumn);
                                        output.Set(this.IsDanglingColumn, false);
                                        break;
                                    case "_label_probability":
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.ProbabilityColumn);
                                        break;
                                    case "_label_Action":
                                        Helpers.ExtractPropertyInteger(jsonReader, output, this.ActionColumn);
                                        break;
                                    case "a":
                                        if (!this.NumActionsColumn.IsRequired)
                                            jsonReader.Skip();
                                        else
                                            output.Set(this.NumActionsColumn.Idx, Helpers.CountArrayElements(jsonReader));
                                        break;
                                    case "o":
                                        if (!this.HasObservationsColumn.IsRequired)
                                            jsonReader.Skip();
                                        else
                                            output.Set(this.HasObservationsColumn.Idx, 1);
                                        break;
                                    case "pdrop":
                                        Helpers.ExtractPropertyDouble(jsonReader, output, this.PdropColumn);
                                        break;
                                    case "_skipLearn":
                                        Helpers.ExtractPropertyBool(jsonReader, output, this.SkipLearnColumn);
                                        break;
                                    case "EnqueuedTimeUtc":
                                        output.Set(this.EnqueuedTimeUtcColumn, (DateTime)jsonReader.ReadAsDateTime());
                                        output.Set(this.IsDanglingColumn, true);
                                        break;
                                    case "RewardValue":
                                        Helpers.ExtractPropertyDoubleOpt(jsonReader, output, this.RewardValueColumn);
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
