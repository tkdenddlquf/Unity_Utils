using System;
using UnityEngine;

namespace Yang.Localize
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class LocalizeAttribute : PropertyAttribute
    {
        public string DataField { get; private set; }

        public LocalizeAttribute(string dataField = "") => DataField = dataField;
    }
}
