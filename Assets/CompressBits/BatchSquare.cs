using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
    public partial struct FoneEConway
    {
        // The bits in a ulong represent an 8x8 grid of cells.
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0
        // 0 0 0 0 0 0 0 0

        // 56 57 58 59 60 61 62 63
        // 48 49 50 51 52 53 54 55
        // 40 41 42 43 44 45 46 47
        // 32 33 34 35 36 37 38 39
        // 24 25 26 27 28 29 30 31
        // 16 17 18 19 20 21 22 23
        //  8  9 10 11 12 13 14 15
        //  0  1  2  3  4  5  6  7

        [BurstCompile]
        public struct UpdateGridJobBatchSquare : IJobParallelForBatch
        {
            public int ArrayElementWidth;
            public int ArrayElementHeight;
            public int ArrayElemetCount;

            [ReadOnly] public NativeArray<ulong> BaseGrid;
            [WriteOnly] public NativeArray<ulong> NewGrid;

            private static int GetCount(ulong value, ulong mask)
            {
                return math.countbits(value & mask);
            }

            private static bool CalculateNewCellValue(ulong value, int index, ulong mask)
            {
                var count = GetCount(value, mask);

                bool isCellAlive = ((value >> index) & 1) == 1;
                bool isNewCellAlive = isCellAlive ? (count == 2 | count == 3) : count == 3;

                return isNewCellAlive;
            }

            public unsafe void Execute(int startIndex, int count)
            {
                var neighborCounts = stackalloc byte[64];

                int startX = startIndex % ArrayElementWidth;
                int startY = startIndex / ArrayElementWidth;

                int x = startX;
                int y = startY;

                for (int index = 0; index < count; index++)
                {
                    int currentIndex = startIndex + index;

                    ulong baseCells = BaseGrid[currentIndex];

                    for (int i = 0; i < 64; i++)
                    {
                        neighborCounts[i] = (byte)math.countbits(baseCells & Tables.NeighborLookups[i]);
                    }

                    bool isLeft = x - 1 >= 0;
                    bool isRight = x + 1 < ArrayElementWidth;
                    bool isTop = y + 1 < ArrayElementHeight;
                    bool isBottom = y - 1 >= 0;

                    var topIndex = currentIndex + ArrayElementWidth;
                    var bottomIndex = currentIndex - ArrayElementWidth;
                    var leftIndex = currentIndex - 1;
                    var rightIndex = currentIndex + 1;

                    var leftTopIndex = topIndex - 1;
                    var rightTopIndex = topIndex + 1;
                    var leftBottomIndex = bottomIndex - 1;
                    var rightBottomIndex = bottomIndex + 1;

                    // If there are cells to the left of this chunk.
                    if (Hint.Likely(isLeft))
                    {
                        var leftMask = BaseGrid[leftIndex];

                        neighborCounts[0] += GetBitValue(leftMask, 7);
                        neighborCounts[0] += GetBitValue(leftMask, 15);

                        neighborCounts[56] += GetBitValue(leftMask, 63);
                        neighborCounts[56] += GetBitValue(leftMask, 55);

                        for (int i = 1; i < 7; i++)
                        {
                            neighborCounts[i * 8] += GetBitValue(leftMask, (i - 1) * 8 + 7);
                            neighborCounts[i * 8] += GetBitValue(leftMask, i * 8 + 7);
                            neighborCounts[i * 8] += GetBitValue(leftMask, (i + 1) * 8 + 7);
                        }

                        // If there are cells to the bottom left of this chunk
                        if (Hint.Likely(isBottom))
                        {
                            neighborCounts[0] += GetBitValue(BaseGrid[leftBottomIndex], 63);
                        }

                        // If there are cells to the top left of this chunk
                        if (Hint.Likely(isTop))
                        {
                            neighborCounts[56] += GetBitValue(BaseGrid[leftTopIndex], 7);
                        }
                    }

                    // If there are cells to the right of this chunk.
                    if (Hint.Likely(isRight))
                    {
                        var rightMask = BaseGrid[rightIndex];

                        neighborCounts[7] += GetBitValue(rightMask, 0);
                        neighborCounts[7] += GetBitValue(rightMask, 8);

                        neighborCounts[63] += GetBitValue(rightMask, 56);
                        neighborCounts[63] += GetBitValue(rightMask, 48);

                        for (int i = 1; i < 7; i++)
                        {
                            neighborCounts[i * 8 + 7] += GetBitValue(rightMask, (i - 1) * 8);
                            neighborCounts[i * 8 + 7] += GetBitValue(rightMask, i * 8);
                            neighborCounts[i * 8 + 7] += GetBitValue(rightMask, (i + 1) * 8);
                        }

                        // If there are cells to the bottom right of this chunk
                        if (Hint.Likely(isBottom))
                        {
                            neighborCounts[7] += GetBitValue(BaseGrid[rightBottomIndex], 56);
                        }

                        // If there are cells to the top right of this chunk
                        if (Hint.Likely(isTop))
                        {
                            neighborCounts[63] += GetBitValue(BaseGrid[rightTopIndex], 0);
                        }
                    }

                    if (Hint.Likely(isTop))
                    {
                        ulong top = BaseGrid[topIndex];

                        neighborCounts[56] += GetBitValue(top, 0);
                        neighborCounts[56] += GetBitValue(top, 1);

                        neighborCounts[63] += GetBitValue(top, 6);
                        neighborCounts[63] += GetBitValue(top, 7);

                        for (int i = 1; i < 7; i++)
                        {
                            neighborCounts[i + 56] += GetBitValue(top, i - 1);
                            neighborCounts[i + 56] += GetBitValue(top, i);
                            neighborCounts[i + 56] += GetBitValue(top, i + 1);
                        }
                    }

                    if (Hint.Likely(isBottom))
                    {
                        ulong bottom = BaseGrid[bottomIndex];

                        neighborCounts[0] += GetBitValue(bottom, 56);
                        neighborCounts[0] += GetBitValue(bottom, 57);

                        neighborCounts[7] += GetBitValue(bottom, 62);
                        neighborCounts[7] += GetBitValue(bottom, 63);

                        for (int i = 1; i < 7; i++)
                        {
                            neighborCounts[i] += GetBitValue(bottom, (i - 1) + 56);
                            neighborCounts[i] += GetBitValue(bottom, i + 56);
                            neighborCounts[i] += GetBitValue(bottom, (i + 1) + 56);
                        }
                    }
                    
                    ulong results = 0;

                    for (int i = 0; i < 64; i++)
                    {
                        bool isCellAlive = IsBitEnabled(baseCells, i);

                        if (isCellAlive)
                        {
                            bool isAlive = neighborCounts[i] == 2 | neighborCounts[i] == 3;

                            results |= (isAlive ? 1UL : 0UL) << i;
                        }
                        else
                        {
                            bool isAlive = neighborCounts[i] == 3;

                            results |= (isAlive ? 1UL : 0UL) << i;
                        }
                    }

                    NewGrid[currentIndex] = results;

                    x++;
                    if (x >= ArrayElementWidth)
                    {
                        y++;
                        x = 0;
                    }

                    UnsafeUtility.MemClear(neighborCounts, UnsafeUtility.SizeOf<byte>() * 64);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsBitEnabled(ulong value, int index)
            {
                return ((value >> index) & 1) == 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetBitValue(ulong value, int index)
            {
                return IsBitEnabled(value, index) ? (byte)1 : (byte)0;
            }
        }

    }
}

 public static class Tables
    {
        public static readonly ulong[] NeighborLookups = new ulong[64]
        {
            0b1100000010,
            0b11100000101,
            0b111000001010,
            0b1110000010100,
            0b11100000101000,
            0b111000001010000,
            0b1110000010100000,
            0b1100000001000000,
            0b110000001000000011,
            0b1110000010100000111,
            0b11100000101000001110,
            0b111000001010000011100,
            0b1110000010100000111000,
            0b11100000101000001110000,
            0b111000001010000011100000,
            0b110000000100000011000000,
            0b11000000100000001100000000,
            0b111000001010000011100000000,
            0b1110000010100000111000000000,
            0b11100000101000001110000000000,
            0b111000001010000011100000000000,
            0b1110000010100000111000000000000,
            0b11100000101000001110000000000000,
            0b11000000010000001100000000000000,
            0b1100000010000000110000000000000000,
            0b11100000101000001110000000000000000,
            0b111000001010000011100000000000000000,
            0b1110000010100000111000000000000000000,
            0b11100000101000001110000000000000000000,
            0b111000001010000011100000000000000000000,
            0b1110000010100000111000000000000000000000,
            0b1100000001000000110000000000000000000000,
            0b110000001000000011000000000000000000000000,
            0b1110000010100000111000000000000000000000000,
            0b11100000101000001110000000000000000000000000,
            0b111000001010000011100000000000000000000000000,
            0b1110000010100000111000000000000000000000000000,
            0b11100000101000001110000000000000000000000000000,
            0b111000001010000011100000000000000000000000000000,
            0b110000000100000011000000000000000000000000000000,
            0b11000000100000001100000000000000000000000000000000,
            0b111000001010000011100000000000000000000000000000000,
            0b1110000010100000111000000000000000000000000000000000,
            0b11100000101000001110000000000000000000000000000000000,
            0b111000001010000011100000000000000000000000000000000000,
            0b1110000010100000111000000000000000000000000000000000000,
            0b11100000101000001110000000000000000000000000000000000000,
            0b11000000010000001100000000000000000000000000000000000000,
            0b1100000010000000110000000000000000000000000000000000000000,
            0b11100000101000001110000000000000000000000000000000000000000,
            0b111000001010000011100000000000000000000000000000000000000000,
            0b1110000010100000111000000000000000000000000000000000000000000,
            0b11100000101000001110000000000000000000000000000000000000000000,
            0b111000001010000011100000000000000000000000000000000000000000000,
            0b1110000010100000111000000000000000000000000000000000000000000000,
            0b1100000001000000110000000000000000000000000000000000000000000000,
            0b1000000011000000000000000000000000000000000000000000000000,
            0b10100000111000000000000000000000000000000000000000000000000,
            0b101000001110000000000000000000000000000000000000000000000000,
            0b1010000011100000000000000000000000000000000000000000000000000,
            0b10100000111000000000000000000000000000000000000000000000000000,
            0b101000001110000000000000000000000000000000000000000000000000000,
            0b1010000011100000000000000000000000000000000000000000000000000000,
            0b100000011000000000000000000000000000000000000000000000000000000,
        };
    }