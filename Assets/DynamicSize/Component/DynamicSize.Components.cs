using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace DynamicSize.Component
{
    public struct GoLPosition: IComponentData
    {
        public int2 Position;
    }
    
    
    public struct CurrentCells: IComponentData,IEnableableComponent
    {
        public ulong Value;
    }
    
    public struct TriggerGoLInitialize: IComponentData
    { }

    public struct InitialSpawn : IComponentData
    {
        public int Width;
        public int Height;
    }
    
    [MaterialProperty("_HighBits")]
    public struct RenderingHighBits: IComponentData
    {
        public int Value;
    }
    
    [MaterialProperty("_LowBits")]
    public struct RenderingLowBits: IComponentData
    {
        public int Value;
    }
    public struct NextCells: IComponentData
    {
        public ulong Value;
    }
   
  
    
}