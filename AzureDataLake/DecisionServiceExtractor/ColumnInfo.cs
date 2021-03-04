using System;
using Microsoft.Analytics.Interfaces;

namespace DecisionServiceExtractor
{
    internal class ColumnInfo
    {
        private int idx = -1;

        public int Idx => idx;

        public bool IsRequired => (idx >= 0);

        public ColumnInfo(ISchema schema, string name, Type type)
        {
            this.idx = schema.IndexOf(name);

            if (idx >= 0)
            {
                var column = schema[idx];
                if (column.IsReadOnly || column.Type != type)
                {
                    this.idx = -1;
                }
            }
        }
    }
}
