using System;
using UnityEngine;

namespace GameUp.Core
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ReadOnlyInInspectorAttribute : PropertyAttribute
    {
    }
}
