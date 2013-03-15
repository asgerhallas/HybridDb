using System;
using System.Collections.Generic;

namespace HybridDb.Tests.Diffing
{
    public static class Differ
    {
        public static List<Delta> Diffs(byte[] original, byte[] modified)
        {
            var deltas = new List<Delta>();

            var originalStart = -1;
            var modifiedStart = -1;

            var originalIndex = 0;
            var modifiedIndex = 0;

            while (true)
            {
                if (originalIndex == original.Length)
                    break;

                if (original[originalIndex] == modified[modifiedIndex])
                {
                    if (originalStart != -1)
                    {
                        var deltaLength = modifiedIndex - modifiedStart;
                        var data = new byte[deltaLength];
                        Array.ConstrainedCopy(modified, modifiedStart, data, 0, deltaLength);
                        deltas.Add(new Delta
                        {
                            Position = originalStart,
                            Length = originalIndex - originalStart,
                            Data = data
                        });
                        originalStart = -1;
                        modifiedStart = -1;
                    }

                    originalIndex++;
                    modifiedIndex++;
                    continue;
                }

                originalStart = originalIndex;
                modifiedStart = modifiedIndex;

                modifiedIndex++;
            }

            return deltas;
        }

        public class Delta
        {
            public int Position { get; set; }
            public int Length { get; set; }
            public byte[] Data { get; set; }
        }
    }

/*
 * Copyright 2008 Google Inc. All Rights Reserved.
 * Author: fraser@google.com (Neil Fraser)
 * Author: anteru@developer.shelter13.net (Matthaeus G. Chajdas)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Diff Match and Patch
 * http://code.google.com/p/google-diff-match-patch/
 */

/**-
   * The data structure representing a diff is a List of Diff objects:
   * {Diff(Operation.DELETE, "Hello"), Diff(Operation.INSERT, "Goodbye"),
   *  Diff(Operation.EQUAL, " world.")}
   * which means: delete "Hello", add "Goodbye" and keep " world."
   */


/**
   * Class representing one diff operation.
   */


/**
   * Class representing one patch operation.
   */


/**
   * Class containing the diff, match and patch methods.
   * Also Contains the behaviour settings.
   */
}