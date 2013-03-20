using System;
using System.Collections.Generic;
using System.Text;
using HybridDb.Diffing;
using Xunit;
using System.Linq;
using Shouldly;

namespace HybridDb.Tests
{
    public class DiffTests
    {
        [Fact]
        public void ReplaceAtStart()
        {
            var a = Encoding.ASCII.GetBytes("asger heller hallas");
            var b = Encoding.ASCII.GetBytes("peter pedal heller halas");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
        }

        [Fact]
        public void RemoveAtStart()
        {
            var a = Encoding.ASCII.GetBytes("asger heller hallas lars udengaard");
            var b = Encoding.ASCII.GetBytes("lars udengaard");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
        }

        [Fact]
        public void DoubleCopy()
        {
            var a = Encoding.ASCII.GetBytes("asger heller hallas lars udengaard");
            var b = Encoding.ASCII.GetBytes("lars udengaard udengaard");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
        }
        
        [Fact]
        public void DoubleCopy2()
        {
            var a = Encoding.ASCII.GetBytes("ghijklmnxxxxxxxxghijklmn");
            var b = Encoding.ASCII.GetBytes("ghijklmnghijklmnghijklmn");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
        }
        
        [Fact]
        public void CopySeparatedByInserts()
        {
            var a = Encoding.ASCII.GetBytes("ghijklmnxxxxxxxxghijklmnghijklmn");
            var b = Encoding.ASCII.GetBytes("ghijklmnhanshanshansghijklmnhanshansghijklmn");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
        }

        [Fact]
        public void CopyWidestPossibleForward()
        {
            var a = Encoding.ASCII.GetBytes("ghijklmnghijklmn");
            var b = Encoding.ASCII.GetBytes("ghijklmnghijklmn");

            var delta = Differ.CalculateDelta(a, b).Single();
            delta.ShouldBeTypeOf<Differ.Copy>();
        }

        [Fact]
        public void AutoCopyBeginningAndEndIfEqual()
        {
            var a = Encoding.ASCII.GetBytes("ghijasgerklmn");
            var b = Encoding.ASCII.GetBytes("ghijlarsklmn");

            var delta = Differ.CalculateDelta(a, b).ToList();
            delta.First().ShouldBeTypeOf<Differ.Copy>();
            delta.Last().ShouldBeTypeOf<Differ.Copy>();
        }

        [Fact]
        public void OneChangeHasCorrectNumberOfInstructions()
        {
            var a = Encoding.ASCII.GetBytes("12345678123456781234567812345678123456781234567812345678123456781234567812345678");
            var b = Encoding.ASCII.GetBytes("12345678123456781234567812345678123456A81234567812345678123456781234567812345678");

            AssertPatch(a, b, Differ.CalculateDelta(a, b));
            

            var delta = Differ.CalculateDelta(a, b).ToList();
            delta.First().ShouldBeTypeOf<Differ.Copy>();
            delta.Last().ShouldBeTypeOf<Differ.Copy>();
        }

        byte[] Patch(byte[] original, IEnumerable<Differ.SqlWrite> instructions)
        {
            var bytes = original.ToList();

            foreach (var instruction in instructions)
            {
                bytes.RemoveRange(instruction.Offset, instruction.Length ?? bytes.Count - instruction.Offset);
                if (instruction.Data != null) 
                    bytes.InsertRange(instruction.Offset, instruction.Data);
            }

            return bytes.ToArray();
        }

        byte[] ApplyPatch(byte[] original, IEnumerable<object> instructions)
        {
            var target = new byte[0];

            int i = 0;
            foreach (var instruction in instructions)
            {
                var copy = instruction as Differ.Copy;
                if (copy != null)
                {
                    Array.Resize(ref target, target.Length + copy.Length);
                    Array.Copy(original, copy.Offset, target, i, copy.Length);
                    i += copy.Length;
                }

                var insert = instruction as Differ.Insert;
                if (insert != null)
                {
                    Array.Resize(ref target, target.Length + 1);
                    target[i] = insert.Data;
                    i++;
                }
            }

            return target;
        }

        void AssertPatch(byte[] original, byte[] modified, IEnumerable<object> diffs)
        {
            diffs = diffs.ToList();

            Console.WriteLine("Original: {0}", Encoding.ASCII.GetString(original));
            
            var expected = Encoding.ASCII.GetString(modified);
            Console.WriteLine("Expected: {0}", expected);

            Console.WriteLine();
            Console.WriteLine("Deltas:");
            foreach (var diff in diffs)
            {
                Console.WriteLine(diff);
            }

            Console.WriteLine();
            Console.WriteLine("Sql diffs:");
            var deltas = Differ.Squash(diffs).ToList();
            foreach (var diff in deltas)
            {
                Console.WriteLine(diff);
            }

            Console.WriteLine();
            Console.WriteLine("Patch 1:");
            Console.WriteLine(Encoding.ASCII.GetString(ApplyPatch(original, diffs)));

            Console.WriteLine();
            Console.WriteLine("Patch 2:");
            Console.WriteLine(Encoding.ASCII.GetString(Patch(original, deltas)));

            Encoding.ASCII.GetString(Patch(original, deltas)).ShouldBe(expected);
        }
    }
}