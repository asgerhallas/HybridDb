using System;
using System.Collections.Generic;
using System.Text;

namespace HybridDb.Tests.Diffing
{
    public static class ArrayEx
    {
        public static byte[] Substring(this byte[] bytes, long offset)
        {
            return Substring(bytes, offset, bytes.Length - offset);
        }

        public static byte[] Substring(this byte[] bytes, long offset, long length)
        {
            var result = new byte[length];
            Array.Copy(bytes, offset, result, 0, length);
            return result;
        }

        public static byte[] Concat(this byte[] bytes, byte[] other)
        {
            var length = bytes.Length + other.Length;
            var result = new byte[length];
            Array.Copy(bytes, 0, result, 0, bytes.Length);
            Array.Copy(other, 0, result, bytes.Length, other.Length);
            return result;
        }

        public static byte[] Concat(this byte[] bytes, byte other)
        {
            return Concat(bytes, new[] {other});
        }

        public static int FindIndexOf(this byte[] bytes, byte[] needle)
        {
            return FindIndexOf(bytes, needle, 0);
        }

        public static int FindIndexOf(this byte[] bytes, byte[] needle, int startIndex)
        {
            if (needle.Length > bytes.Length)
                return -1;

            var found = 0;
            for (int i = startIndex; i < bytes.Length; i++)
            {
                if (bytes[i] != needle[i])
                {
                    found = 0;
                    continue;
                }
                
                found++;
                if (found == needle.Length)
                    return i - found;
            }

            return -1;
        }
    }

    public class diff_match_patch
    {
        // Defaults.
        // Set these on your diff_match_patch instance to override the defaults.

        // Number of seconds to map a diff before giving up (0 for infinity).
        public float Diff_Timeout = 1.0f;

        // Cost of an empty edit operation in terms of edit characters.
        public short Diff_EditCost = 4;

        // At what point is no match declared (0.0 = perfection, 1.0 = very loose).
        public float Match_Threshold = 0.5f;

        // How far to search for a match (0 = exact location, 1000+ = broad match).
        // A match this many characters away from the expected location will add
        // 1.0 to the score (0.0 is a perfect match).
        public int Match_Distance = 1000;

        // When deleting a large block of text (over ~64 characters), how close
        // do the contents have to be to match the expected contents. (0.0 =
        // perfection, 1.0 = very loose).  Note that Match_Threshold controls
        // how closely the end points of a delete need to match.
        public float Patch_DeleteThreshold = 0.5f;

        // Chunk size for context length.
        public short Patch_Margin = 4;

        // The number of bits in an int.
        short Match_MaxBits = 32;


        //  DIFF FUNCTIONS


        /**
     * Find the differences between two texts.
     * Run a faster, slightly less optimal diff.
     * @param text1 Old string to be diffed.
     * @param text2 New string to be diffed.
     * @return List of Diff objects.
     */

        public List<Diff> diff_main(byte[] text1, byte[] text2)
        {
            // Set a deadline by which time the diff must be complete.
            DateTime deadline;
            if (Diff_Timeout <= 0)
            {
                deadline = DateTime.MaxValue;
            }
            else
            {
                deadline = DateTime.Now + new TimeSpan(((long) (Diff_Timeout*1000))*10000);
            }

            return diff_main(text1, text2, deadline);
        }

        /**
     * Find the differences between two texts.  Simplifies the problem by
     * stripping any common prefix or suffix off the texts before diffing.
     * @param text1 Old string to be diffed.
     * @param text2 New string to be diffed.
     * @param checklines Speedup flag.  If false, then don't run a
     *     line-level diff first to identify the changed areas.
     *     If true, then run a faster slightly less optimal diff.
     * @param deadline Time when the diff should be complete by.  Used
     *     internally for recursive calls.  Users should set DiffTimeout
     *     instead.
     * @return List of Diff objects.
     */

        List<Diff> diff_main(byte[] text1, byte[] text2, DateTime deadline)
        {
            // Check for null inputs not needed since null can't be passed in C#.

            // Check for equality (speedup).
            List<Diff> diffs;
            if (text1 == text2)
            {
                diffs = new List<Diff>();
                if (text1.Length != 0)
                {
                    diffs.Add(new Diff(Operation.EQUAL, text1));
                }
                return diffs;
            }

            // Trim off common prefix (speedup).
            var commonlength = diff_commonPrefix(text1, text2);
            var commonprefix = text1.Substring(0, commonlength);

            text1 = text1.Substring(commonlength);
            text2 = text2.Substring(commonlength);

            // Trim off common suffix (speedup).
            commonlength = diff_commonSuffix(text1, text2);
            var commonsuffix = text1.Substring(text1.Length - commonlength);
            text1 = text1.Substring(0, text1.Length - commonlength);
            text2 = text2.Substring(0, text2.Length - commonlength);

            // Compute the diff on the middle block.
            diffs = diff_compute(text1, text2, deadline);

            // Restore the prefix and suffix.
            if (commonprefix.Length != 0)
            {
                diffs.Insert(0, (new Diff(Operation.EQUAL, commonprefix)));
            }
            if (commonsuffix.Length != 0)
            {
                diffs.Add(new Diff(Operation.EQUAL, commonsuffix));
            }

            diff_cleanupMerge(diffs);
            return diffs;
        }

        /**
     * Find the differences between two texts.  Assumes that the texts do not
     * have any common prefix or suffix.
     * @param text1 Old string to be diffed.
     * @param text2 New string to be diffed.
     * @param deadline Time when the diff should be complete by.
     * @return List of Diff objects.
     */

        List<Diff> diff_compute(byte[] text1, byte[] text2, DateTime deadline)
        {
            var diffs = new List<Diff>();

            if (text1.Length == 0)
            {
                // Just add some text (speedup).
                diffs.Add(new Diff(Operation.INSERT, text2));
                return diffs;
            }

            if (text2.Length == 0)
            {
                // Just delete some text (speedup).
                diffs.Add(new Diff(Operation.DELETE, text1));
                return diffs;
            }

            var longtext = text1.Length > text2.Length ? text1 : text2;
            var shorttext = text1.Length > text2.Length ? text2 : text1;
            var i = longtext.FindIndexOf(shorttext);
            if (i != -1)
            {
                // Shorter text is inside the longer text (speedup).
                var op = (text1.Length > text2.Length)
                             ? Operation.DELETE
                             : Operation.INSERT;
                diffs.Add(new Diff(op, longtext.Substring(0, i)));
                diffs.Add(new Diff(Operation.EQUAL, shorttext));
                diffs.Add(new Diff(op, longtext.Substring(i + shorttext.Length)));
                return diffs;
            }

            if (shorttext.Length == 1)
            {
                // Single character string.
                // After the previous speedup, the character can't be an equality.
                diffs.Add(new Diff(Operation.DELETE, text1));
                diffs.Add(new Diff(Operation.INSERT, text2));
                return diffs;
            }

            // Check to see if the problem can be split in two.
            var hm = diff_halfMatch(text1, text2);
            if (hm != null)
            {
                // A half-match was found, sort out the return data.
                var text1_a = hm[0];
                var text1_b = hm[1];
                var text2_a = hm[2];
                var text2_b = hm[3];
                var mid_common = hm[4];
                // Send both pairs off for separate processing.
                var diffs_a = diff_main(text1_a, text2_a, deadline);
                var diffs_b = diff_main(text1_b, text2_b, deadline);
                // Merge the results.
                diffs = diffs_a;
                diffs.Add(new Diff(Operation.EQUAL, mid_common));
                diffs.AddRange(diffs_b);
                return diffs;
            }

            return diff_bisect(text1, text2, deadline);
        }

        /**
     * Find the 'middle snake' of a diff, split the problem in two
     * and return the recursively constructed diff.
     * See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
     * @param text1 Old string to be diffed.
     * @param text2 New string to be diffed.
     * @param deadline Time at which to bail if not yet complete.
     * @return List of Diff objects.
     */

        protected List<Diff> diff_bisect(byte[] text1, byte[] text2,
                                         DateTime deadline)
        {
            // Cache the text lengths to prevent multiple calls.
            var text1_length = text1.Length;
            var text2_length = text2.Length;
            var max_d = (text1_length + text2_length + 1)/2;
            var v_offset = max_d;
            var v_length = 2*max_d;
            var v1 = new int[v_length];
            var v2 = new int[v_length];
            for (var x = 0; x < v_length; x++)
            {
                v1[x] = -1;
                v2[x] = -1;
            }
            v1[v_offset + 1] = 0;
            v2[v_offset + 1] = 0;
            var delta = text1_length - text2_length;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            var front = (delta%2 != 0);
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            var k1start = 0;
            var k1end = 0;
            var k2start = 0;
            var k2end = 0;
            for (var d = 0; d < max_d; d++)
            {
                // Bail out if deadline is reached.
                if (DateTime.Now > deadline)
                {
                    break;
                }

                // Walk the front path one step.
                for (var k1 = -d + k1start; k1 <= d - k1end; k1 += 2)
                {
                    var k1_offset = v_offset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1_offset - 1] < v1[k1_offset + 1])
                    {
                        x1 = v1[k1_offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1_offset - 1] + 1;
                    }
                    var y1 = x1 - k1;
                    while (x1 < text1_length && y1 < text2_length
                           && text1[x1] == text2[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1_offset] = x1;
                    if (x1 > text1_length)
                    {
                        // Ran off the right of the graph.
                        k1end += 2;
                    }
                    else if (y1 > text2_length)
                    {
                        // Ran off the bottom of the graph.
                        k1start += 2;
                    }
                    else if (front)
                    {
                        var k2_offset = v_offset + delta - k1;
                        if (k2_offset >= 0 && k2_offset < v_length && v2[k2_offset] != -1)
                        {
                            // Mirror x2 onto top-left coordinate system.
                            var x2 = text1_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (var k2 = -d + k2start; k2 <= d - k2end; k2 += 2)
                {
                    var k2_offset = v_offset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2_offset - 1] < v2[k2_offset + 1])
                    {
                        x2 = v2[k2_offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2_offset - 1] + 1;
                    }
                    var y2 = x2 - k2;
                    while (x2 < text1_length && y2 < text2_length
                           && text1[text1_length - x2 - 1]
                           == text2[text2_length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2_offset] = x2;
                    if (x2 > text1_length)
                    {
                        // Ran off the left of the graph.
                        k2end += 2;
                    }
                    else if (y2 > text2_length)
                    {
                        // Ran off the top of the graph.
                        k2start += 2;
                    }
                    else if (!front)
                    {
                        var k1_offset = v_offset + delta - k2;
                        if (k1_offset >= 0 && k1_offset < v_length && v1[k1_offset] != -1)
                        {
                            var x1 = v1[k1_offset];
                            var y1 = v_offset + x1 - k1_offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = text1_length - v2[k2_offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }
            }
            // Diff took too long and hit the deadline or
            // number of diffs equals number of characters, no commonality at all.
            var diffs = new List<Diff>();
            diffs.Add(new Diff(Operation.DELETE, text1));
            diffs.Add(new Diff(Operation.INSERT, text2));
            return diffs;
        }

        /**
     * Given the location of the 'middle snake', split the diff in two parts
     * and recurse.
     * @param text1 Old string to be diffed.
     * @param text2 New string to be diffed.
     * @param x Index of split point in text1.
     * @param y Index of split point in text2.
     * @param deadline Time at which to bail if not yet complete.
     * @return LinkedList of Diff objects.
     */

        List<Diff> diff_bisectSplit(byte[] text1, byte[] text2,
                                    int x, int y, DateTime deadline)
        {
            var text1a = text1.Substring(0, x);
            var text2a = text2.Substring(0, y);
            var text1b = text1.Substring(x);
            var text2b = text2.Substring(y);

            // Compute both diffs serially.
            var diffs = diff_main(text1a, text2a, deadline);
            var diffsb = diff_main(text1b, text2b, deadline);

            diffs.AddRange(diffsb);
            return diffs;
        }

        /**
     * Determine the common prefix of two strings.
     * @param text1 First string.
     * @param text2 Second string.
     * @return The number of characters common to the start of each string.
     */

        public int diff_commonPrefix(byte[] text1, byte[] text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            var n = Math.Min(text1.Length, text2.Length);
            for (var i = 0; i < n; i++)
            {
                if (text1[i] != text2[i])
                {
                    return i;
                }
            }
            return n;
        }

        /**
     * Determine the common suffix of two strings.
     * @param text1 First string.
     * @param text2 Second string.
     * @return The number of characters common to the end of each string.
     */

        public int diff_commonSuffix(byte[] text1, byte[] text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            var n = Math.Min(text1.Length, text2.Length);
            for (var i = 1; i <= n; i++)
            {
                if (text1[text1.Length - i] != text2[text2.Length - i])
                {
                    return i - 1;
                }
            }
            return n;
        }

        /**
     * Determine if the suffix of one string is the prefix of another.
     * @param text1 First string.
     * @param text2 Second string.
     * @return The number of characters common to the end of the first
     *     string and the start of the second string.
     */

        protected int diff_commonOverlap(byte[] text1, byte[] text2)
        {
            // Cache the text lengths to prevent multiple calls.
            var text1_length = text1.Length;
            var text2_length = text2.Length;
            // Eliminate the null case.
            if (text1_length == 0 || text2_length == 0)
            {
                return 0;
            }
            // Truncate the longer string.
            if (text1_length > text2_length)
            {
                text1 = text1.Substring(text1_length - text2_length);
            }
            else if (text1_length < text2_length)
            {
                text2 = text2.Substring(0, text1_length);
            }
            var text_length = Math.Min(text1_length, text2_length);
            // Quick check for the worst case.
            if (text1 == text2)
            {
                return text_length;
            }

            // Start by looking for a single character match
            // and increase length until no match is found.
            // Performance analysis: http://neil.fraser.name/news/2010/11/04/
            var best = 0;
            var length = 1;
            while (true)
            {
                var pattern = text1.Substring(text_length - length);
                var found = text2.FindIndexOf(pattern);
                if (found == -1)
                {
                    return best;
                }
                length += found;
                if (found == 0 || text1.Substring(text_length - length) == text2.Substring(0, length))
                {
                    best = length;
                    length++;
                }
            }
        }

        /**
     * Do the two texts share a Substring which is at least half the length of
     * the longer text?
     * This speedup can produce non-minimal diffs.
     * @param text1 First string.
     * @param text2 Second string.
     * @return Five element String array, containing the prefix of text1, the
     *     suffix of text1, the prefix of text2, the suffix of text2 and the
     *     common middle.  Or null if there was no match.
     */

        protected byte[][] diff_halfMatch(byte[] text1, byte[] text2)
        {
            if (Diff_Timeout <= 0)
            {
                // Don't risk returning a non-optimal diff if we have unlimited time.
                return null;
            }
            var longtext = text1.Length > text2.Length ? text1 : text2;
            var shorttext = text1.Length > text2.Length ? text2 : text1;
            if (longtext.Length < 4 || shorttext.Length*2 < longtext.Length)
            {
                return null; // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            var hm1 = diff_halfMatchI(longtext, shorttext,
                                      (longtext.Length + 3)/4);
            // Check again based on the third quarter.
            var hm2 = diff_halfMatchI(longtext, shorttext,
                                      (longtext.Length + 1)/2);
            byte[][] hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            else if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                // Both matched.  Select the longest.
                hm = hm1[4].Length > hm2[4].Length ? hm1 : hm2;
            }

            // A half-match was found, sort out the return data.
            if (text1.Length > text2.Length)
            {
                return hm;
                //return new string[]{hm[0], hm[1], hm[2], hm[3], hm[4]};
            }
            else
            {
                return new[] {hm[2], hm[3], hm[0], hm[1], hm[4]};
            }
        }

        /**
     * Does a Substring of shorttext exist within longtext such that the
     * Substring is at least half the length of longtext?
     * @param longtext Longer string.
     * @param shorttext Shorter string.
     * @param i Start index of quarter length Substring within longtext.
     * @return Five element string array, containing the prefix of longtext, the
     *     suffix of longtext, the prefix of shorttext, the suffix of shorttext
     *     and the common middle.  Or null if there was no match.
     */

        byte[][] diff_halfMatchI(byte[] longtext, byte[] shorttext, int i)
        {
            // Start with a 1/4 length Substring at position i as a seed.
            var seed = longtext.Substring(i, longtext.Length/4);
            var j = -1;
            var best_common = new byte[0];
            var best_longtext_a = new byte[0];
            var best_longtext_b = new byte[0];
            var best_shorttext_a = new byte[0];
            var best_shorttext_b = new byte[0];

            while (j < shorttext.Length && (j = shorttext.FindIndexOf(seed, j + 1)) != -1)
            {
                var prefixLength = diff_commonPrefix(longtext.Substring(i),
                                                     shorttext.Substring(j));
                var suffixLength = diff_commonSuffix(longtext.Substring(0, i),
                                                     shorttext.Substring(0, j));
                if (best_common.Length < suffixLength + prefixLength)
                {
                    best_common = shorttext.Substring(j - suffixLength, suffixLength).Concat(shorttext.Substring(j, prefixLength));
                    best_longtext_a = longtext.Substring(0, i - suffixLength);
                    best_longtext_b = longtext.Substring(i + prefixLength);
                    best_shorttext_a = shorttext.Substring(0, j - suffixLength);
                    best_shorttext_b = shorttext.Substring(j + prefixLength);
                }
            }
            if (best_common.Length*2 >= longtext.Length)
            {
                return new[]
                {
                    best_longtext_a, best_longtext_b,
                    best_shorttext_a, best_shorttext_b, best_common
                };
            }
            else
            {
                return null;
            }
        }

        /**
     * Reduce the number of edits by eliminating semantically trivial
     * equalities.
     * @param diffs List of Diff objects.
     */

        public void diff_cleanupSemantic(List<Diff> diffs)
        {
            var changes = false;
            // Stack of indices where equalities are found.
            var equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            byte[] lastequality = null;
            var pointer = 0; // Index of current position.
            // Number of characters that changed prior to the equality.
            var length_insertions1 = 0;
            var length_deletions1 = 0;
            // Number of characters that changed after the equality.
            var length_insertions2 = 0;
            var length_deletions2 = 0;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {
                    // Equality found.
                    equalities.Push(pointer);
                    length_insertions1 = length_insertions2;
                    length_deletions1 = length_deletions2;
                    length_insertions2 = 0;
                    length_deletions2 = 0;
                    lastequality = diffs[pointer].data;
                }
                else
                {
                    // an insertion or deletion
                    if (diffs[pointer].operation == Operation.INSERT)
                    {
                        length_insertions2 += diffs[pointer].data.Length;
                    }
                    else
                    {
                        length_deletions2 += diffs[pointer].data.Length;
                    }
                    // Eliminate an equality that is smaller or equal to the edits on both
                    // sides of it.
                    if (lastequality != null && (lastequality.Length
                                                 <= Math.Max(length_insertions1, length_deletions1))
                        && (lastequality.Length
                            <= Math.Max(length_insertions2, length_deletions2)))
                    {
                        // Duplicate record.
                        diffs.Insert(equalities.Peek(),
                                     new Diff(Operation.DELETE, lastequality));
                        // Change second copy to insert.
                        diffs[equalities.Peek() + 1].operation = Operation.INSERT;
                        // Throw away the equality we just deleted.
                        equalities.Pop();
                        if (equalities.Count > 0)
                        {
                            equalities.Pop();
                        }
                        pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                        length_insertions1 = 0; // Reset the counters.
                        length_deletions1 = 0;
                        length_insertions2 = 0;
                        length_deletions2 = 0;
                        lastequality = null;
                        changes = true;
                    }
                }
                pointer++;
            }

            // Normalize the diff.
            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
            //diff_cleanupSemanticLossless(diffs);

            // Find any overlaps between deletions and insertions.
            // e.g: <del>abcxxx</del><ins>xxxdef</ins>
            //   -> <del>abc</del>xxx<ins>def</ins>
            // e.g: <del>xxxabc</del><ins>defxxx</ins>
            //   -> <ins>def</ins>xxx<del>abc</del>
            // Only extract an overlap if it is as big as the edit ahead or behind it.
            pointer = 1;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer - 1].operation == Operation.DELETE &&
                    diffs[pointer].operation == Operation.INSERT)
                {
                    var deletion = diffs[pointer - 1].data;
                    var insertion = diffs[pointer].data;
                    var overlap_length1 = diff_commonOverlap(deletion, insertion);
                    var overlap_length2 = diff_commonOverlap(insertion, deletion);
                    if (overlap_length1 >= overlap_length2)
                    {
                        if (overlap_length1 >= deletion.Length/2.0 ||
                            overlap_length1 >= insertion.Length/2.0)
                        {
                            // Overlap found.
                            // Insert an equality and trim the surrounding edits.
                            diffs.Insert(pointer, new Diff(Operation.EQUAL,
                                                           insertion.Substring(0, overlap_length1)));
                            diffs[pointer - 1].data =
                                deletion.Substring(0, deletion.Length - overlap_length1);
                            diffs[pointer + 1].data = insertion.Substring(overlap_length1);
                            pointer++;
                        }
                    }
                    else
                    {
                        if (overlap_length2 >= deletion.Length/2.0 ||
                            overlap_length2 >= insertion.Length/2.0)
                        {
                            // Reverse overlap found.
                            // Insert an equality and swap and trim the surrounding edits.
                            diffs.Insert(pointer, new Diff(Operation.EQUAL,
                                                           deletion.Substring(0, overlap_length2)));
                            diffs[pointer - 1].operation = Operation.INSERT;
                            diffs[pointer - 1].data =
                                insertion.Substring(0, insertion.Length - overlap_length2);
                            diffs[pointer + 1].operation = Operation.DELETE;
                            diffs[pointer + 1].data = deletion.Substring(overlap_length2);
                            pointer++;
                        }
                    }
                    pointer++;
                }
                pointer++;
            }
        }

        /**
     * Reduce the number of edits by eliminating operationally trivial
     * equalities.
     * @param diffs List of Diff objects.
     */

        public void diff_cleanupEfficiency(List<Diff> diffs)
        {
            var changes = false;
            // Stack of indices where equalities are found.
            var equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            var lastequality = new byte[0];
            var pointer = 0; // Index of current position.
            // Is there an insertion operation before the last equality.
            var pre_ins = false;
            // Is there a deletion operation before the last equality.
            var pre_del = false;
            // Is there an insertion operation after the last equality.
            var post_ins = false;
            // Is there a deletion operation after the last equality.
            var post_del = false;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {
                    // Equality found.
                    if (diffs[pointer].data.Length < Diff_EditCost
                        && (post_ins || post_del))
                    {
                        // Candidate found.
                        equalities.Push(pointer);
                        pre_ins = post_ins;
                        pre_del = post_del;
                        lastequality = diffs[pointer].data;
                    }
                    else
                    {
                        // Not a candidate, and can never become one.
                        equalities.Clear();
                        lastequality = new byte[0];
                    }
                    post_ins = post_del = false;
                }
                else
                {
                    // An insertion or deletion.
                    if (diffs[pointer].operation == Operation.DELETE)
                    {
                        post_del = true;
                    }
                    else
                    {
                        post_ins = true;
                    }
                    /*
           * Five types to be split:
           * <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
           * <ins>A</ins>X<ins>C</ins><del>D</del>
           * <ins>A</ins><del>B</del>X<ins>C</ins>
           * <ins>A</del>X<ins>C</ins><del>D</del>
           * <ins>A</ins><del>B</del>X<del>C</del>
           */
                    if ((lastequality.Length != 0)
                        && ((pre_ins && pre_del && post_ins && post_del)
                            || ((lastequality.Length < Diff_EditCost/2)
                                && ((pre_ins ? 1 : 0) + (pre_del ? 1 : 0) + (post_ins ? 1 : 0)
                                    + (post_del ? 1 : 0)) == 3)))
                    {
                        // Duplicate record.
                        diffs.Insert(equalities.Peek(),
                                     new Diff(Operation.DELETE, lastequality));
                        // Change second copy to insert.
                        diffs[equalities.Peek() + 1].operation = Operation.INSERT;
                        equalities.Pop(); // Throw away the equality we just deleted.
                        lastequality = new byte[0];
                        if (pre_ins && pre_del)
                        {
                            // No changes made which could affect previous entry, keep going.
                            post_ins = post_del = true;
                            equalities.Clear();
                        }
                        else
                        {
                            if (equalities.Count > 0)
                            {
                                equalities.Pop();
                            }

                            pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                            post_ins = post_del = false;
                        }
                        changes = true;
                    }
                }
                pointer++;
            }

            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
        }

        /**
     * Reorder and merge like edit sections.  Merge equalities.
     * Any edit section can move as long as it doesn't cross an equality.
     * @param diffs List of Diff objects.
     */

        public void diff_cleanupMerge(List<Diff> diffs)
        {
            // Add a dummy entry at the end.
            diffs.Add(new Diff(Operation.EQUAL, new byte[0]));
            var pointer = 0;
            var count_delete = 0;
            var count_insert = 0;
            var text_delete = new byte[0];
            var text_insert = new byte[0];
            int commonlength;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        count_insert++;
                        text_insert = text_insert.Concat(diffs[pointer].data);
                        pointer++;
                        break;
                    case Operation.DELETE:
                        count_delete++;
                        text_delete = text_delete.Concat(diffs[pointer].data);
                        pointer++;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (count_delete + count_insert > 1)
                        {
                            if (count_delete != 0 && count_insert != 0)
                            {
                                // Factor out any common prefixies.
                                commonlength = diff_commonPrefix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    if ((pointer - count_delete - count_insert) > 0 && diffs[pointer - count_delete - count_insert - 1].operation == Operation.EQUAL)
                                    {
                                        var diff = diffs[pointer - count_delete - count_insert - 1];
                                        diff.data = diff.data.Concat(text_insert.Substring(0, commonlength));
                                    }
                                    else
                                    {
                                        diffs.Insert(0, new Diff(Operation.EQUAL, text_insert.Substring(0, commonlength)));
                                        pointer++;
                                    }
                                    text_insert = text_insert.Substring(commonlength);
                                    text_delete = text_delete.Substring(commonlength);
                                }
                                // Factor out any common suffixies.
                                commonlength = diff_commonSuffix(text_insert, text_delete);
                                if (commonlength != 0)
                                {
                                    diffs[pointer].data = text_insert.Substring(text_insert.Length - commonlength).Concat(diffs[pointer].data);
                                    text_insert = text_insert.Substring(0, text_insert.Length
                                                                           - commonlength);
                                    text_delete = text_delete.Substring(0, text_delete.Length
                                                                           - commonlength);
                                }
                            }
                            // Delete the offending records and add the merged ones.
                            if (count_delete == 0)
                            {
                                diffs.Splice(pointer - count_insert,
                                             count_delete + count_insert,
                                             new Diff(Operation.INSERT, text_insert));
                            }
                            else if (count_insert == 0)
                            {
                                diffs.Splice(pointer - count_delete,
                                             count_delete + count_insert,
                                             new Diff(Operation.DELETE, text_delete));
                            }
                            else
                            {
                                diffs.Splice(pointer - count_delete - count_insert,
                                             count_delete + count_insert,
                                             new Diff(Operation.DELETE, text_delete),
                                             new Diff(Operation.INSERT, text_insert));
                            }
                            pointer = pointer - count_delete - count_insert +
                                      (count_delete != 0 ? 1 : 0) + (count_insert != 0 ? 1 : 0) + 1;
                        }
                        else if (pointer != 0 && diffs[pointer - 1].operation == Operation.EQUAL)
                        {
                            // Merge this equality with the previous one.
                            diffs[pointer - 1].data = diffs[pointer - 1].data.Concat(diffs[pointer].data);
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }
                        count_insert = 0;
                        count_delete = 0;
                        text_delete = new byte[0];
                        text_insert = new byte[0];
                        break;
                }
            }
            if (diffs[diffs.Count - 1].data.Length == 0)
            {
                diffs.RemoveAt(diffs.Count - 1); // Remove the dummy entry at the end.
            }

            //// Second pass: look for single edits surrounded on both sides by
            //// equalities which can be shifted sideways to eliminate an equality.
            //// e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            //var changes = false;
            //pointer = 1;
            //// Intentionally ignore the first and last element (don't need checking).
            //while (pointer < (diffs.Count - 1))
            //{
            //    if (diffs[pointer - 1].operation == Operation.EQUAL &&
            //        diffs[pointer + 1].operation == Operation.EQUAL)
            //    {
            //        // This is a single edit surrounded by equalities.
            //        if (diffs[pointer].data.EndsWith(diffs[pointer - 1].data, StringComparison.Ordinal))
            //        {
            //            // Shift the edit over the previous equality.
            //            diffs[pointer].data = diffs[pointer - 1].data +
            //                                  diffs[pointer].data.Substring(0, diffs[pointer].data.Length -
            //                                                                   diffs[pointer - 1].data.Length);
            //            diffs[pointer + 1].data = diffs[pointer - 1].data
            //                                      + diffs[pointer + 1].data;
            //            diffs.Splice(pointer - 1, 1);
            //            changes = true;
            //        }
            //        else if (diffs[pointer].data.StartsWith(diffs[pointer + 1].data, StringComparison.Ordinal))
            //        {
            //            // Shift the edit over the next equality.
            //            diffs[pointer - 1].data += diffs[pointer + 1].data;
            //            diffs[pointer].data =
            //                diffs[pointer].data.Substring(diffs[pointer + 1].data.Length)
            //                + diffs[pointer + 1].data;
            //            diffs.Splice(pointer + 1, 1);
            //            changes = true;
            //        }
            //    }
            //    pointer++;
            //}
            // If shifts were made, the diff needs reordering and another shift sweep.
            //if (changes)
            //{
            //    diff_cleanupMerge(diffs);
            //}
        }

        /**
     * loc is a location in text1, comAdde and return the equivalent location in
     * text2.
     * e.g. "The cat" vs "The big cat", 1->1, 5->8
     * @param diffs List of Diff objects.
     * @param loc Location within text1.
     * @return Location within text2.
     */

        public int diff_xIndex(List<Diff> diffs, int loc)
        {
            var chars1 = 0;
            var chars2 = 0;
            var last_chars1 = 0;
            var last_chars2 = 0;
            Diff lastDiff = null;
            foreach (var aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    // Equality or deletion.
                    chars1 += aDiff.data.Length;
                }
                if (aDiff.operation != Operation.DELETE)
                {
                    // Equality or insertion.
                    chars2 += aDiff.data.Length;
                }
                if (chars1 > loc)
                {
                    // Overshot the location.
                    lastDiff = aDiff;
                    break;
                }
                last_chars1 = chars1;
                last_chars2 = chars2;
            }
            if (lastDiff != null && lastDiff.operation == Operation.DELETE)
            {
                // The location was deleted.
                return last_chars2;
            }
            // Add the remaining character length.
            return last_chars2 + (loc - last_chars1);
        }

        /**
     * Compute and return the source text (all equalities and deletions).
     * @param diffs List of Diff objects.
     * @return Source text.
     */

        public string diff_text1(List<Diff> diffs)
        {
            var text = new StringBuilder();
            foreach (var aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    text.Append(aDiff.data);
                }
            }
            return text.ToString();
        }

        /**
     * Compute and return the destination text (all equalities and insertions).
     * @param diffs List of Diff objects.
     * @return Destination text.
     */

        public string diff_text2(List<Diff> diffs)
        {
            var text = new StringBuilder();
            foreach (var aDiff in diffs)
            {
                if (aDiff.operation != Operation.DELETE)
                {
                    text.Append(aDiff.data);
                }
            }
            return text.ToString();
        }

        /**
     * Compute the Levenshtein distance; the number of inserted, deleted or
     * substituted characters.
     * @param diffs List of Diff objects.
     * @return Number of changes.
     */

        public int diff_levenshtein(List<Diff> diffs)
        {
            var levenshtein = 0;
            var insertions = 0;
            var deletions = 0;
            foreach (var aDiff in diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        insertions += aDiff.data.Length;
                        break;
                    case Operation.DELETE:
                        deletions += aDiff.data.Length;
                        break;
                    case Operation.EQUAL:
                        // A deletion and an insertion is one substitution.
                        levenshtein += Math.Max(insertions, deletions);
                        insertions = 0;
                        deletions = 0;
                        break;
                }
            }
            levenshtein += Math.Max(insertions, deletions);
            return levenshtein;
        }
    }
}