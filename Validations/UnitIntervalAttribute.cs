using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace DecisionServicePrivateWeb.Validations
{
    public class UnitIntervalAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return false;
            }
            float floatValue = -1;
            bool isFloat = float.TryParse(value.ToString(), out floatValue);

            return isFloat && floatValue >= 0 && floatValue <= 1;
        }
    }
}