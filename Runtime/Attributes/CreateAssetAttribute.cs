using System;

namespace ScriptableFlow.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CreateAssetAttribute : Attribute
    {
        public readonly bool inherit;
        public readonly string category;
        
        public CreateAssetAttribute(bool inherit = false, string category = "None")
        {
            this.inherit = inherit;
            this.category = category;
        }
    }
}