using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.TrivialJob
{
    public class GoLSimulator : MonoBehaviour
    {
        public uint2 gridSize;

        private NativeArray<bool> gridBack;
        private NativeArray<bool> gridFront;

        public NativeArray<bool> Grid => Tick % 2 == 1 ? gridFront : gridBack;

        public uint Tick = 0;
        public float TickTime = 0.5f;

        private float tickTimer = 0;

        // Start is called before the first frame update
        void Start()
        {
            gridBack = new NativeArray<bool>((int)gridSize.x * (int)gridSize.y, Allocator.Persistent);
            gridFront = new NativeArray<bool>((int)gridSize.x * (int)gridSize.y, Allocator.Persistent);

            for (int i = 0; i < gridBack.Length; i++)
            {
                gridBack[i] = UnityEngine.Random.value > 0.5f;
            }
            /* var center = (int)(gridSize.x * gridSize.y/2)+(int)gridSize.x/2;
             for (var i = 0; i < 3; i++)
             {
                 gridBack[center + i] = true;
             }*/

        }

        // Update is called once per frame
        void Update()
        {
            // Debug.Log($"Frame: {Time.frameCount}, backSum: {gridBack.Sum(b => b? 1: 0)}, frontSum, {gridFront.Sum(b => b? 1: 0)}");
            tickTimer -= Time.deltaTime;
            if (tickTimer > 0) return;
            tickTimer = TickTime;

            Tick++;
            var job = new NextGenerationJob
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                gridSize = gridSize
            };
            job.Schedule(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }

        private void OnDestroy()
        {
            gridBack.Dispose();
            gridFront.Dispose();
        }
    }


    [BurstCompile]
    public partial struct NextGenerationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<bool> grid;

        [WriteOnly] public NativeArray<bool> nextGrid;
        [ReadOnly] public uint2 gridSize;

        [BurstCompile]
        public void Execute(int index)
        {
            var count = CountNeighbors((uint)index);

            if (grid[index])
            {
                nextGrid[index] = count == 2 || count == 3;
            }
            else
            {
                nextGrid[index] = count == 3;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CountNeighbors(uint index)
        {
            var co = GetGridCoordinate(index);
            return GetCell(co.x - 1, co.y - 1) +
                   GetCell(co.x, co.y - 1) +
                   GetCell(co.x + 1, co.y - 1) +
                   GetCell(co.x - 1, co.y) +
                   GetCell(co.x + 1, co.y) +
                   GetCell(co.x - 1, co.y + 1) +
                   GetCell(co.x, co.y + 1) +
                   GetCell(co.x + 1, co.y + 1);
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte GetCell(uint x, uint y)
        {
            if (x >= gridSize.x || y >= gridSize.y)
                return 0;
            return grid.Reinterpret<byte>()[Index(x, y)];
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(uint x, uint y)
        {
            return (int)(y * gridSize.x + x);
        }


        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint2 GetGridCoordinate(uint index)
        {
            return new uint2(index % gridSize.x, index / gridSize.x);
        }
    }
}