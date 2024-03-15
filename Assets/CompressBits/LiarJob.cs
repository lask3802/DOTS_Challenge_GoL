using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
    [BurstCompile]
    public partial struct LiarJob : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [WriteOnly] public NativeArray<GoLCells> nextGrid;
        [ReadOnly] public uint2 gridSize;
        [ReadOnly] public int2 gridSizeInCompressed;


        /* square bit layout, just show edges to figure out how edge case works
         *
         * 56 57 58 59 60 61 62 63
         * 48                   55
         * 40                   47
         * 32                   39
         * 24                   31
         * 16  ...              23
         * 8                    15
         * 0 1 2 3 4 5 6         7
         */


        /* neighbor layout
         * 20 21 22 23 24 25 26 27
         * 13                   19
         * 12                   18
         * 11                   17
         * 10                   16
         * 9                    15
         * 8                    14
         * 0 1 2 3 4 5 6        7
         */


        [BurstCompile]
        public unsafe void Execute(int startIndex, int count)
        {
            var neighbors = stackalloc byte[28];


            const ulong northMask = 0x7;
            const ulong southMask = 0x07_00_00_00_00_00_00_00;
            const ulong westMask = 0x808080;
            const ulong eastMask = 0x010101;
            const ulong northWestMask = 0x80;
            const ulong northEastMask = 0x1;
            const ulong southWestMask = 0x80_00_00_00_00_00_00_00;
            const ulong southEastMask = 0x01_00_00_00_00_00_00_00;


            int startX = (int)(startIndex % gridSizeInCompressed.x);
            int startY = (int)(startIndex / gridSizeInCompressed.x);

            int x = startX;
            int y = startY;

            for (int idx = 0; idx < count; idx++)
            {
                var index = startIndex + idx;
                var cell = grid[index].cells;

                bool hasWest = x - 1 >= 0;
                bool hasEast = x + 1 < gridSizeInCompressed.x;
                bool hasNorth = y + 1 < gridSizeInCompressed.y;
                bool hasSouth = y - 1 >= 0;

                var northIndex = (index + gridSizeInCompressed.x);
                var southIndex = (index - gridSizeInCompressed.x);
                var westIndex = index - 1;
                var eastIndex = index + 1;

                var northWestIndex = northIndex - 1;
                var northEastIndex = northIndex + 1;
                var southWestIndex = southIndex - 1;
                var southEastIndex = southIndex + 1;

                UnsafeUtility.MemClear(neighbors, 28);


                for (int neighborIdx = 0; neighborIdx < 28; neighborIdx++)
                {
                    neighbors[neighborIdx] =
                        CountBits(cell & Tables.NeighborLookups[NeighborIndex.ToBitIndex(neighborIdx)]);
                }
                
                
                if (Hint.Likely(hasWest))
                {
                    var west = GetValue(westIndex);
                    neighbors[0] += (byte)(GetBit(west, 7) + GetBit(west, 15));
                    
                    //general east side 
                    for (int neighborIdx = 8; neighborIdx < 14; neighborIdx++)
                    {
                        //Debug.Log($"east bitIndex: {bitIndex}");
                        neighbors[neighborIdx]+=CountBits(west & (westMask << (8 * (neighborIdx - 8))));
                    }
                    //bitIndex 56
                    neighbors[20] += (byte)(GetBit(west, 55) + GetBit(west, 63));
                }
                if (Hint.Likely(hasSouth))
                {
                    var south = GetValue(southIndex);
                    neighbors[0] += (byte)(GetBit(south, 56) + GetBit(south, 57));
                    //general south side 
                    for (int neighborIdx = 1; neighborIdx < 7; neighborIdx++)
                    {
                        //Debug.Log($"south bitIndex: {bitIndex}");
                        neighbors[neighborIdx]+=CountBits(south & (southMask <<  (neighborIdx - 1)));
                    }
                    neighbors[7] += (byte)(GetBit(south, 62) + GetBit(south, 63));
                }
                
                if (Hint.Likely(hasSouth && hasWest))
                {
                    var southWest = GetValue(southWestIndex);
                    neighbors[0] += CountBits(southWest & southWestMask);
                }
                if(Hint.Likely(hasSouth && hasEast))
                {
                    var southEast = GetValue(southEastIndex);
                    neighbors[7] += CountBits(southEast & southEastMask);
                }
                if(Hint.Likely(hasEast))
                {
                    var east = GetValue(eastIndex);
                    neighbors[7] += (byte)(GetBit(east, 0) + GetBit(east, 8));
                    
                    //general west side 
                    for (int neighborIdx = 14; neighborIdx < 20; neighborIdx++)
                    {
                        //Debug.Log($"west bitIndex: {bitIndex}");
                        neighbors[neighborIdx]+=CountBits(east & (eastMask << (8 * (neighborIdx-14))));
                    }
                    //bitIndex 63
                    neighbors[27] += (byte)(GetBit(east, 56) + GetBit(east, 48));
                }
                if(Hint.Likely(hasNorth && hasWest))
                {
                    var northWest = GetValue(northWestIndex);
                    //bitIndex 56
                    neighbors[20] += CountBits(northWest & northWestMask);
                }
                if(Hint.Likely(hasNorth && hasEast))
                {
                    var northEast = GetValue(northEastIndex);
                    neighbors[27] += CountBits(northEast & northEastMask);
                }
                if (Hint.Likely(hasNorth))
                {
                    var north = GetValue(northIndex);
                    //bitIndex 56
                    neighbors[20] += (byte)(GetBit(north, 0) + GetBit(north, 1));
                    //general north side 
                    for (int neighborIdx = 21; neighborIdx < 27; neighborIdx++)
                    {
                        //var bitIndex = NeighborIndex.ToBitIndex(neighborIdx);
                        //Debug.Log($"$north bitIndex: {bitIndex}");
                        var shiftedMask = (northMask << (neighborIdx - 21));
                        var countBits = CountBits(north & shiftedMask);
                        /*if (north != 0)
                        {
                            Debug.Log($"north: {north:0X} shiftedMask:{shiftedMask:0X} bitIndex: {bitIndex} countBits: {countBits}");
                        }*/
                        neighbors[neighborIdx]+=countBits;
                    }
                    //bitIndex 63
                    neighbors[27] += (byte)(GetBit(north, 6) + GetBit(north, 7));
                }
               
               
                ulong val = 0ul;
               /* bool logThisIndex = false;
                for (int i = 0; i < 28; i++)
                {
                    if (neighbors[i] != 0)
                    {
                        Debug.Log($"index {index}");
                        logThisIndex = true;
                        break;
                    }
                }*/

                for(int i = 0; i < 28; i++)
                {
                   /* if(logThisIndex)
                        Debug.Log($"{i}:{neighbors[i]}");*/
                    
                    var bitIndex = NeighborIndex.ToBitIndex(i);
                    var cellState = GetBit(cell, bitIndex);
                    var is3 = neighbors[i] == 3;
                    var is2 = neighbors[i] == 2;
                    var isAlive = cellState == 1 ? (is2 || is3) : is3;
                    val |= isAlive ? 1ul << bitIndex : 0;
                    /*if (cellState == 1)
                    {
                        bool isAlive = neighbors[i] == 2 || neighbors[i] == 3;
                        val |= isAlive ? 1ul << bitIndex : 0;
                    }
                    else
                    {
                        bool isAlive = neighbors[i] == 3;
                        val |= isAlive ? 1ul << bitIndex : 0;
                    }*/
                }
                //Debug.Log(debug);
                    
                //val &= ~(0x007E7E7E7E7E7E00ul);
                val |= Liar(cell);
                nextGrid[index] = new GoLCells { cells = val };

                //move to next block
                x++;
                if (x >= gridSizeInCompressed.x)
                {
                    y++;
                    x = 0;
                }
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void REDUCE(out ulong g, out ulong f, ulong a, ulong b, ulong c)
        {
            var d = a ^ b;
            var e = b ^ c;
            f = c ^ d;
            g = d | e;
        }

        /*
#define REDUCE(g,f,a,b,c) do {	\
        uint64_t d, e;	\
        d = a ^ b;	\
        e = b ^ c;	\
        f = c ^ d;	\
        g = d | e;	\
    } while(0)
*/
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong Liar(ulong bmp)
        {
            ulong tmp, left, right;
            ulong upper1, side1, mid1, lower1;
            ulong upper2, side2, mid2, lower2;
            ulong sum12, sum13, sum24, sum26;
            // this wraps around the left and right edges
            // but incorrect results for the border are ok
            left = bmp << 1;
            right = bmp >> 1;
            // half adder count of cells to either side
            side1 = left ^ right;
            side2 = left & right;
            // full adder count of cells in middle row
            mid1 = side1 ^ bmp;
            mid2 = side1 & bmp;
            mid2 = side2 | mid2;
            // shift middle row count to get upper and lower row counts
            upper1 = mid1 << 8;
            lower1 = mid1 >> 8;
            upper2 = mid2 << 8;
            lower2 = mid2 >> 8;
            // compress vertically
            REDUCE(out sum12, out sum13, upper1, side1, lower1);
            REDUCE(out sum24, out sum26, upper2, side2, lower2);
            // calculate result
            tmp = sum12 ^ sum13;
            bmp = (bmp | sum13) & (tmp ^ sum24) & (tmp ^ sum26);
            // mask out incorrect border cells
            return (bmp & 0x007E7E7E7E7E7E00);
            // total 19 + 7 = 26 operations
        }


        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasNorth(int index)
        {
            return GetY(index) + 1 < gridSizeInCompressed.y;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasSouth(int index)
        {
            return GetY(index) > 0;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasWest(int index)
        {
            return GetX(index) > 0;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasEast(int index)
        {
            return GetX(index) + 1 < gridSizeInCompressed.x;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WestIndex(int index)
        {
            return index - 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int EastIndex(int index)
        {
            return index + 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthIndex(int index)
        {
            return index + (int)gridSizeInCompressed.x;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthIndex(int index)
        {
            return index - (int)gridSizeInCompressed.x;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthWestIndex(int index)
        {
            return NorthIndex(index) - 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NorthEastIndex(int index)
        {
            return NorthIndex(index) + 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthWestIndex(int index)
        {
            return SouthIndex(index) - 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int SouthEastIndex(int index)
        {
            return SouthIndex(index) + 1;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetValue(int index)
        {
            return grid[index].cells;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetValue(uint index)
        {
            return grid[(int)index].cells;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte CountBits(ulong value)
        {
            if (!X86.Popcnt.IsPopcntSupported)
                return (byte)math.countbits(value);
            return (byte)X86.Popcnt.popcnt_u64(value);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetBit(ref ulong value, int bit)
        {
            value |= 1ul << bit;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetBit(ulong value, int bit)
        {
            return (byte)((value >> bit) & 1);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetX(int index)
        {
            return index % gridSizeInCompressed.x;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetY(int index)
        {
            return index / gridSizeInCompressed.x;
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
}