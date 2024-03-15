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
    public partial struct SquareLayoutJob : IJobParallelForBatch
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

        [BurstCompile]
        public unsafe void Execute(int startIndex, int count)
        {
            //Neighbor array helps vectorized counting neighbors
            //this way is much fast then in-place alive check
            //inspired from FoneE's implementation
            var neighbors = stackalloc byte[64];


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

                //In value bit counting
                for (int i = 0; i < 64; i++)
                {
                    neighbors[i] = CountBits(cell & Tables.NeighborLookups[i]);
                }

                if (Hint.Likely(hasWest))
                {
                    var west = GetValue(westIndex);

                    //Left Bottom and Left Up corner
                    if (Hint.Likely(hasSouth))
                    {
                        neighbors[0] += CountBits(GetValue(southWestIndex) & southWestMask);
                    }

                    neighbors[0] += (byte)(GetBit(west, 7) + GetBit(west, 15));

                    //Left internal edge
                    for (int i = 1; i < 7; i++)
                    {
                        neighbors[i*8] += CountBits(west & (westMask << (8 * (i-1))));
                    }

                    neighbors[56] += (byte)(GetBit(west, 55) + GetBit(west, 63));
                    if (Hint.Likely(hasNorth))
                    {
                        neighbors[56] += CountBits(GetValue(northWestIndex) & northWestMask);
                    }
                }

               
                if (Hint.Likely(hasNorth))
                {
                    var north = GetValue(northIndex);

                    neighbors[56] += (byte)(GetBit(north, 0) + GetBit(north, 1));
                    
                    for (int i = 1; i < 7; i++)
                    {
                        neighbors[56 + i] += CountBits(north & (northMask << (i - 1)));
                    }

                    neighbors[63] += (byte)(GetBit(north, 6) + GetBit(north, 7));
                }

                if (Hint.Likely(hasEast))
                {
                    var east = GetValue(eastIndex);

                    //Left Bottom and Left Up corner
                    if (Hint.Likely(hasSouth))
                    {
                        neighbors[7] += CountBits(GetValue(southEastIndex) & southEastMask);
                    }

                    neighbors[7] += (byte)(GetBit(east, 0) + GetBit(east, 8));

                    
                    for (int i = 1; i < 7; i++)
                    {
                        neighbors[i * 8 + 7] += CountBits(east & (eastMask << (8 * (i - 1))));
                    }


                    neighbors[63] += (byte)(GetBit(east, 56) + GetBit(east, 48));
                    
                    if (Hint.Likely(hasNorth))
                    {
                        neighbors[63] += CountBits(GetValue(northEastIndex) & northEastMask);
                    }
                }
               

                if (Hint.Likely(hasSouth))
                {
                    var south = GetValue(southIndex);
                    
                    neighbors[0] += (byte)(GetBit(south, 56) + GetBit(south, 57));
                    for (int i = 1; i < 7; i++)
                    {
                        neighbors[i] += CountBits(south & (southMask <<  (i - 1)));
                    }
                    neighbors[7] += (byte)(GetBit(south, 62) + GetBit(south, 63));
                }
                
               
                var val = 0ul;
                //Almost fully vectorized by Burst. inspired from FoneE's implementation
                for (int i = 0; i < 64; i++)
                {
                    //ulong mask = 1ul << i;
                    bool isSet = (cell & (1ul << i)) > 0;
                    if (isSet)
                    {
                        bool isAlive = neighbors[i] == 2 || neighbors[i] == 3;
                        //bool isAlive = (neighbors[i] & 6) == 2;
                        val |= isAlive ? (1ul << i) : 0;
                    }
                    else
                    {
                        bool isAlive = neighbors[i] == 3;
                        val |= isAlive ? (1ul << i) : 0;
                    }
                }

                nextGrid[index] = new GoLCells { cells = val };
                
                //move to next block
                x++;
                if (x >= gridSizeInCompressed.x)
                {
                    y++;
                    x = 0;
                }
                UnsafeUtility.MemClear(neighbors, 64);
            }
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


    
    
}