using DynamicSize.Component;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DynamicSize.Authoring
{
    public class GoLConfigAuthoring : MonoBehaviour
    {
        public GameObject CellsPrefab;
        public int2 InitialGridSize = new int2(1024,1024);
        public float TickTime = 1f;
        public bool IsPaused;
       
        private class GoLConfigAuthoringBaker : Baker<GoLConfigAuthoring>
        {
            public override void Bake(GoLConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var cellsPrefab = GetEntity(authoring.CellsPrefab, TransformUsageFlags.Dynamic );
                
                
                AddComponent(entity, new GoLConfig { CellsPrefab = cellsPrefab, InitialGridSize = authoring.InitialGridSize});
                AddComponent(entity, new GoLState { TickTime = authoring.TickTime, IsPaused = authoring.IsPaused});
                AddComponent<TriggerGoLInitialize>(entity);
            }
        }
    }
}