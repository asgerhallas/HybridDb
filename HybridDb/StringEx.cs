using System;
using System.Text;

namespace HybridDb
{
    public static class StringEx
    {
        public static string Replace(this string str, string oldValue, string newValue, StringComparison comparison)
        {
            var sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static string ToSql(this Parameter parameter)
        {
            var value = parameter.Value;

            if (value == null)
                return "NULL";

            if (value is Boolean)
                return ((bool) value) ? "1" : "0";
            
            if (value is String || value is Guid)
                return "'" + value + "'";
            
            return value.ToString();
        }


    }
}