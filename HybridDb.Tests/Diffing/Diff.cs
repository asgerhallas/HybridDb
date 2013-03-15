using System;
using System.Text;

namespace HybridDb.Tests.Diffing
{
    public class Diff
    {
        public Operation operation; // One of: INSERT, DELETE or EQUAL.
        public byte[] data; // The text associated with this diff operation.

        /**
     * Constructor.  Initializes the diff with the provided values.
     * @param operation One of INSERT, DELETE or EQUAL.
     * @param text The text being applied.
     */

        public Diff(Operation operation, byte[] data)
        {
            // Construct a diff with the specified operation and text.
            this.operation = operation;
            this.data = data;
        }

        /**
     * Display a human-readable version of this Diff.
     * @return text version.
     */

        public override string ToString()
        {
            var prettyText = Encoding.ASCII.GetString(data);
            return "Diff(" + operation + ",\"" + prettyText + "\")";
        }

        /**
     * Is this Diff equivalent to another Diff?
     * @param d Another Diff to compare against.
     * @return true or false.
     */

        public override bool Equals(Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Diff return false.
            var p = obj as Diff;
            if (p == null)
            {
                return false;
            }

            // Return true if the fields match.
            return p.operation == operation && p.data == data;
        }

        public bool Equals(Diff obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // Return true if the fields match.
            return obj.operation == operation && obj.data == data;
        }

        public override int GetHashCode()
        {
            return data.GetHashCode() ^ operation.GetHashCode();
        }
    }
}