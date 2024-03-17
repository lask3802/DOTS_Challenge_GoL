using DynamicSize.Component;
using DynamicSize.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace DynamicSize.System
{
    [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))]
    [CreateAfter(typeof(InitializeGoLSystem))]
    [UpdateAfter(typeof(InitializeGoLSystem))]
    [UpdateBefore(typeof(GoLGenerationSystem))]
    public partial struct CellSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoLConfig>();
            state.RequireForUpdate<GoLState>();
            state.RequireForUpdate<CellsManagement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var golConfig = SystemAPI.GetSingleton<GoLConfig>();
            var management = SystemAPI.GetSingleton<CellsManagement>();
            
            
            //spawn touched cells
            var profilerMarker = new ProfilerMarker("SpawnNewCells");
            
            //Debug.Log($"Cells to spawn: {management.CellsToSpawn.Length}");
            using (profilerMarker.Auto())
            {
                
                var sampleEntity = state.EntityManager.Instantiate(golConfig.CellsPrefab);
                GoLSpawnHelper.AddCellComponents(ref state, ref sampleEntity);
                var newCellEntities = CollectionHelper.CreateNativeArray<Entity>(management.CellsToSpawn.Length, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
                state.EntityManager.Instantiate(sampleEntity, newCellEntities);
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(sampleEntity);
                state.EntityManager.DestroyEntity(sampleEntity);
                
                //only few cells (under thousands) to spawn, just spawn it in main thread
                for(var i = 0; i < newCellEntities.Length; i++)
                {
                    var position = management.CellsToSpawn[i];
                    var e = newCellEntities[i];
                    SystemAPI.SetComponent(e, new GoLPosition {Position = position});
                    localTransform.Position.xy = position * Constants.WorldCellSize;
                    SystemAPI.SetComponent(e, localTransform);
                    SystemAPI.SetComponent(e, new CurrentCells());
                    management.ActiveCells.TryAdd(position, e);
                }
                management.CellsToSpawn.Clear();
            }
                /*new SetNewCellsJob
                {
                    Entities = newCellEntities,
                    Positions = management.CellsToSpawn.AsArray(),
                    PositionLookup = SystemAPI.GetComponentLookup<GoLPosition>(),
                    TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(),
                    CellsLookup = SystemAPI.GetComponentLookup<CurrentCells>(),
                    ActiveCells = management.ActiveCells.AsParallelWriter(),
                    DefaultTransform = localTransform
                }.Schedule(newCellEntities.Length , state.Dependency).Complete();*/
               
                
                /*
                var localTransform = state.EntityManager.GetComponentData<LocalTransform>(golConfig.CellsPrefab);
                foreach (var position in management.CellsToSpawn)
                {
                    var e= state.EntityManager.Instantiate(golConfig.CellsPrefab);
                    GoLSpawnHelper.AddCellComponents(ref state, ref e);
                    SystemAPI.SetComponent(e, new GoLPosition {Position = position});

                    localTransform.Position.xy = position * Constants.WorldCellSize;
                    SystemAPI.SetComponent(e, localTransform);
                
                    management.ActiveCells.TryAdd(position, e);
                    //Debug.Log($"Spawned cell at {position}");
                }*/
            
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
    
    [BurstCompile(Debug = true)]
    public partial struct SetNewCellsJob:IJobFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<int2> Positions;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<GoLPosition> PositionLookup;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [NativeDisableParallelForRestriction][WriteOnly] public ComponentLookup<CurrentCells> CellsLookup;
        [WriteOnly] public NativeParallelHashMap<int2, Entity>.ParallelWriter ActiveCells;
        public LocalTransform DefaultTransform;
        
        [BurstCompile]
        public void Execute(int index)
        {
            var entity = Entities[index];
            var position = Positions[index];
            var transform = DefaultTransform;
            
            PositionLookup.GetRefRW(entity).ValueRW = new GoLPosition
            {
                Position = position
            };
            transform.Position.xy = position * Constants.WorldCellSize;
            TransformLookup.GetRefRW(entity).ValueRW = transform;
            CellsLookup.GetRefRW(entity).ValueRW = new CurrentCells
            {
                Value = 0
            };
            ActiveCells.TryAdd(position, entity);
        }
    }
}