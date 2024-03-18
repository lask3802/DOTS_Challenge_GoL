using System;
using DynamicSize.Component;
using DynamicSize.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace DynamicSize.System
{
    [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))]
   
    public partial struct InitializeGoLSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoLConfig>();
            state.RequireForUpdate<GoLState>();
            state.RequireForUpdate<TriggerGoLInitialize>();
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<CellsManagement>(e);
            SystemAPI.SetSingleton(
                new CellsManagement
                {
                    ActiveCells = new NativeParallelHashMap<int2, Entity>(1024, Allocator.Persistent),
                    CellsToSpawn = new NativeList<int2>(1024, Allocator.Persistent),
                    
                });
            
            state.RequireForUpdate<CellsManagement>();
        }

        [BurstCompile]
        public unsafe void OnUpdate(ref SystemState state)
        {
            var goLConfig = SystemAPI.GetSingleton<GoLConfig>();
            var cellsManagement = SystemAPI.GetSingleton<CellsManagement>();
            
            if(goLConfig.CellsPrefab == Entity.Null)
                throw new ArgumentException("GoLConfig.Cells prefab is not set");

            //clean up custom cell records
            cellsManagement.ActiveCells.Clear();
            cellsManagement.CellsToSpawn.Clear();
            //clean up cell entities
            state.EntityManager.DestroyEntity(SystemAPI.QueryBuilder().WithAll<CurrentCells>().Build());
            
            // Do the initial spawn
            var transform = state.EntityManager.GetComponentData<LocalTransform>(goLConfig.CellsPrefab);
            var rand = Random.CreateFromIndex((uint)Time.realtimeSinceStartup * 12354);
            
            var width = goLConfig.InitialGridSize.x/Constants.CellsWidth;
            var height = goLConfig.InitialGridSize.x/Constants.CellHeight;
            
            var sampleEntity = state.EntityManager.Instantiate(goLConfig.CellsPrefab);
            GoLSpawnHelper.AddCellComponents(ref state, ref sampleEntity);
            
            // Spawn the initial grid and it's neighbors, width+2 and height+2 to account for the neighbors
            var entitiesToSpawn = CollectionHelper.CreateNativeArray<Entity>((width+2)*(height+2), state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
            
            state.EntityManager.Instantiate(sampleEntity, entitiesToSpawn);
            cellsManagement.ActiveCells.Capacity = math.max(entitiesToSpawn.Length*8,cellsManagement.ActiveCells.Capacity);
            Debug.Log($"Entities to spawn: {entitiesToSpawn.Length}, width: {width}, height: {height}");
            var initJob = new EntityInitialSpawnJob
            {
                Entities = entitiesToSpawn,
                PositionLookup = SystemAPI.GetComponentLookup<GoLPosition>(),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
                CellsLookup = SystemAPI.GetComponentLookup<CurrentCells>(),
                ActiveCells = cellsManagement.ActiveCells.AsParallelWriter(),
                Width = width + 2,
                Height = height + 2,
                defaultTransform = transform,
                rand = rand
            }.ScheduleParallel(entitiesToSpawn.Length, width + 2, state.Dependency);
            initJob.Complete();

            var triggerEntity = SystemAPI.GetSingletonEntity<TriggerGoLInitialize>();
            state.EntityManager.RemoveComponent<TriggerGoLInitialize>(triggerEntity);

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            var e = SystemAPI.GetSingletonEntity<CellsManagement>();
            var cellsManagement = SystemAPI.GetSingleton<CellsManagement>();
            cellsManagement.Dispose();
            state.EntityManager.RemoveComponent<CellsManagement>(e);
            state.EntityManager.DestroyEntity(e);
            Debug.Log("InitGoLSystem Destroyed");
        }
    }

    [BurstCompile]
    public partial struct EntityInitialSpawnJob : IJobParallelForBatch
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<GoLPosition> PositionLookup;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<CurrentCells> CellsLookup;
        [WriteOnly] public NativeParallelHashMap<int2, Entity>.ParallelWriter ActiveCells;
        public int Width;
        public int Height;
        public LocalTransform defaultTransform;
        public Random rand;

        public void Execute(int startIndex, int count)
        {
            var transform = defaultTransform;
            rand.state += (uint)startIndex;
            for(int i = 0; i < count; i++)
            {
                var index = startIndex + i;
                var entity = Entities[index];
                
                var x = index % Width;
                var y = index / Width;
                if(x < 1 || x > Width-2 || y < 1 || y > Height-2)
                {
                    continue;
                }
                
                var position = new int2(x,y);
                PositionLookup.GetRefRW(entity).ValueRW = new GoLPosition
                {
                    Position = position
                };
                transform.Position.xy = position * Constants.WorldCellSize;
                TransformLookup.GetRefRW(entity).ValueRW = transform;
                CellsLookup.GetRefRW(entity).ValueRW = new CurrentCells
                {
                    Value =((ulong)rand.NextUInt())<<32 | rand.NextUInt()
                };
                ActiveCells.TryAdd(position, entity);
            }
        }
    }
}