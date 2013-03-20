using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HybridDb.Diffing
{
    public static class Differ
    {
        const int s = 8;

        public static IEnumerable<SqlWrite> Squash(IEnumerable<object> instructions)
        {
            int i = 0;
            int deviation = 0;
            var inserts = new List<byte>();
            foreach (var instruction in instructions)
            {
                var insert = instruction as Insert;
                if (insert != null)
                {
                    inserts.Add(insert.Data);
                    deviation++;
                }

                var copy = instruction as Copy;
                if (copy != null)
                {
                    if (inserts.Count > 0)
                        yield return EmitInsert(inserts, ref i);

                    var offset = copy.Offset + deviation;
                    if (offset > i)
                    {
                        yield return new SqlWrite
                        {
                            Offset = i,
                            Length = offset - i
                        };

                        deviation -= offset - i;
                    }

                    i += copy.Length;
                }
            }

            if (inserts.Count > 0)
                yield return EmitInsert(inserts, ref i);

            yield return new SqlWrite
            {
                Offset = i,
                Length = null,
            };
        }

        static SqlWrite EmitInsert(List<byte> inserts, ref int i)
        {
            var delta = new SqlWrite
            {
                Data = inserts.ToArray(),
                Offset = i,
                Length = 0,
            };
            i += inserts.Count;
            inserts.Clear();
            return delta;
        }

        public static IEnumerable<object> CalculateDelta(byte[] source, byte[] target)
        {
            var instructions = new List<object>();

            var i = 0;
            var sourceOffset = 0;
            var index = InitMatch(source);
            while (i < target.Length)
            {
                var match = FindMatch(source, index, sourceOffset, target, i);
                if (match.Length < s)
                {
                    instructions.Add(new Insert
                    {
                        Data = target[i]
                    });
                    
                    i += 1;
                }
                else
                {
                    instructions.Add(new Copy
                    {
                        Offset = match.Offset,
                        Length = match.Length
                    });

                    sourceOffset = match.Offset + match.Length;
                    i += match.Length;
                }
            }

            return instructions;
        }

        public static Dictionary<uint, Stack<int>> InitMatch(byte[] source)
        {
            var i = source.Length - s;
            var index = new Dictionary<uint, Stack<int>>();
            while (i >= 0)
            {
                var f = Adler32Checksum(source, i, i + s);
                
                Stack<int> offsets;
                if (!index.TryGetValue(f, out offsets))
                {
                    index[f] = offsets = new Stack<int>();
                }

                offsets.Push(i);

                i -= s;
            }

            return index;
        }

        public static Region FindMatch(byte[] source, Dictionary<uint, Stack<int>> index, int minOffsetSource, byte[] target, int offsetTarget)
        {
            var f = Adler32Checksum(target, offsetTarget, offsetTarget + s);
            Stack<int> offsetSources;
            if (!index.TryGetValue(f, out offsetSources))
                return new Region { Offset = -1, Length = -1 };

            int offsetSource;
            while (true)
            {
                if (offsetSources.Count == 0)
                    return new Region { Offset = -1, Length = -1 };

                offsetSource = offsetSources.Peek();
                if (offsetSource >= minOffsetSource)
                {
                    break;
                }

                offsetSources.Pop();
            }

            if (!RangeEqual(source, offsetSource, target, offsetTarget, s))
                return new Region { Offset = -1, Length = -1 };

            int i = s;
            while (offsetSource + i < source.Length)
            {
                if (!RangeEqual(source, offsetSource + i, target, offsetTarget + i, s))
                    break;

                i += s;
            }

            return new Region { Offset = offsetSource, Length = i };
        }

        public static bool RangeEqual(byte[] source, int offsetSource, byte[] target, int offsetTarget, int length)
        {
            for (int j = 0; j < length; j++)
            {
                if (offsetSource + j >= source.Length || offsetTarget + j >= target.Length)
                    return false;

                if (source[offsetSource + j] != target[offsetTarget + j])
                    return false;
            }

            return true;
        }

        public class Insert
        {
            public byte Data { get; set; }

            public override string ToString()
            {
                return string.Format("Insert ('{0}')", Encoding.ASCII.GetString(new[] { Data }));
            }
        }

        public class Copy
        {
            public byte[] TheDataForTestsYeah { get; set; }
            public int Offset { get; set; }
            public int Length { get; set; }

            public override string ToString()
            {
                return string.Format("Copy ({0}, {1}", Offset, Length);
            }
        }

        public class Region
        {
            public int Offset { get; set; }
            public int Length { get; set; }
        }

        public class SqlWrite
        {
            public int Offset { get; set; }
            public int? Length { get; set; }
            public byte[] Data { get; set; }

            public override string ToString()
            {
                if (Data == null)
                    return "'', " + Offset + ", " + Length;

                return "'" + Encoding.ASCII.GetString(Data) + "', " + Offset + ", " + Length;
            }
        }

        public static uint Adler32Checksum(byte[] data, int start, int length)
        {
            ushort sum1 = 1;
            ushort sum2 = 0;
            for (int i = start; i < length && i < data.Length; i++)
            {
                sum1 = (ushort) ((sum1 + data[i])%65521);
                sum2 = (ushort) ((sum1 + sum2)%65521);
            }

            // concat the two 16 bit values to form
            // one 32-bit value
            return (uint) ((sum2 << 16) | sum1);
        }


            //var j = 0;
            //while (true)
            //{
            //    if (source[j] != target[j])
            //    {
            //        if (j > 0)
            //        {
            //            instructions.Add(new Copy
            //            {
            //                Offset = 0,
            //                Length = j
            //            });
            //        }

            //        break;
            //    }

            //    j++;
            //}


            //object lastInstruction = null;
            //var g = 0;
            //while (source.Length < g && target.Length < g)
            //{
            //    if (source[source.Length-1-g] != target[target.Length-1-g])
            //    {
            //        if (g > 0)
            //        {
            //            lastInstruction = new Copy
            //            {
            //                Offset = source.Length-g,
            //                Length = g
            //            };
            //        }

            //        break;
            //    }

            //    g++;
            //}

            //if (lastInstruction != null)
            //    instructions.Add(lastInstruction);


    }
}