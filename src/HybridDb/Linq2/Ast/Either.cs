using System;
using System.ComponentModel;

namespace HybridDb.Linq2.Ast
{
    [TypeConverter(typeof(EitherTypeConverter))]
    public abstract class Either<T1, T2>
    {
        public static Either<T1, T2> New(T1 value)
        {
            return new IsA<T1>(value);
        } 

        public static Either<T1, T2> New(T2 value)
        {
            return new IsA<T2>(value);
        } 

        public static implicit operator Either<T1, T2>(T1 left)
        {
            return New(left);
        }

        public static implicit operator Either<T1, T2>(T2 right)
        {
            return New(right);
        }

        public class IsA<T> : Either<T1, T2>, A<T>
        {
            public IsA(T value)
            {
                Value = value;
            }

            public T Value { get; }
        }
    }

    public interface A<T>
    {
        T Value { get; }
    }

    public class EitherTypeConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return base.CanConvertTo(context, destinationType);
        }
    }
}