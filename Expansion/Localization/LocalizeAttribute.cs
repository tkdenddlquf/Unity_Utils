using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class LocalizeAttribute : PropertyAttribute
{
    public string dataField;

    public LocalizeAttribute(string dataField = "") => this.dataField = dataField;
}
