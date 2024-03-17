using Unity.Mathematics;

namespace DynamicSize
{
    public static class Constants
    {
        public const int CellsWidth = 8;
        public const int CellHeight = 8;
        public const int ReferenceRenderTextureSize = 1024;
        public static readonly float2 WorldCellSize = new float2(1f, 1f);
        public static readonly float2 InitialPosition = new float2(0f, 0f);
        
    }
}