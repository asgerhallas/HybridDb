using System;
using System.Text;
using Xunit;
using System.Linq;
using Shouldly;

namespace HybridDb.Tests.Diffing
{
    public class DiffTests
    {
        diff_match_patch diffMatchPatch;

        public DiffTests()
        {
            diffMatchPatch = new diff_match_patch();
        }

        [Fact]
        public void FactMethodName()
        {
            var a = Encoding.ASCII.GetBytes("asger");
            var b = Encoding.ASCII.GetBytes("asgar");

            var delta = diffMatchPatch.diff_main(a, b);
            foreach (var diff in delta)
            {
                Console.WriteLine(diff);
            }
            //delta.Position.ShouldBe(3);
            //delta.Length.ShouldBe(1);
            //Encoding.ASCII.GetString(delta.Data).ShouldBe("a");
        }

        [Fact]
        public void FactMethodName2()
        {
            var a = Encoding.ASCII.GetBytes("asger");
            var b = Encoding.ASCII.GetBytes("asgler");

            var delta = diffMatchPatch.diff_main(a, b); 
            foreach (var diff in delta)
            {
                Console.WriteLine(diff);
            }
            //delta.Position.ShouldBe(3);
            //delta.Length.ShouldBe(0);
            //Encoding.ASCII.GetString(delta.Data).ShouldBe("l");
        }
    }
}