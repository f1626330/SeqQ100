using System;

namespace Sequlite.WPF.Framework
{
    internal class ValueDescription
    {
        public ValueDescription()
        {
        }

        public Enum Value { get; set; }
        public object Description { get; set; }
        public override string ToString()
        {
            return Description?.ToString();
        }
    }
}