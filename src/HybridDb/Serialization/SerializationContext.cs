using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Serialization
{
    public class SerializationContext
    {
        readonly Type[] valueTypes;
        readonly HashSet<object> uniqueSerializedObjects = new HashSet<object>(new ReferenceEqualityComparer<object>());
        readonly Stack<object> ancestors = new Stack<object>();

        public SerializationContext(params Type[] valueTypes)
        {
            this.valueTypes = valueTypes;
        }

        public object Current { get; private set; }

        public object Root { get; private set; }

        public bool HasRoot
        {
            get { return Root != null; }
        }

        public object Parent
        {
            get { return ancestors.Peek(); }
        }

        public bool HasParent
        {
            get { return ancestors.Count > 0; }
        }

        public void Push(object current)
        {
            if (Root == null)
            {
                Root = current;
            }
            else
            {
                ancestors.Push(Current);
            }

            Current = current;
        }

        public object Pop()
        {
            if (ancestors.Count == 0)
                return null;

            return Current = ancestors.Pop();
        }

        public void EnsureNoDuplicates(object value)
        {
            if (uniqueSerializedObjects.Contains(value) && !valueTypes.Contains(value.GetType()))
            {
                throw new InvalidOperationException(string.Format(
                    "Attempted to serialize {0} twice. Please ensure that no instance " +
                    "of an object is referenced from two places in the model.", value));
            }

            uniqueSerializedObjects.Add(value);
        }
    }
}