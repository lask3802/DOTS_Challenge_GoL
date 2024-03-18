Shader "Custom/GoLStaticGrid"
{
	Properties
	{
		_Color("Color", Color) = (0, 0, 0, 0)			
	}
	SubShader
	{
		Tags
		{
			//"RenderPipeline"="UniversalPipeline"
			"RenderType"="Opaque"
			"Queue"="Geometry"
		}
		Pass
		{
			Name "Pass"

			HLSLPROGRAM

			#pragma target 4.5			
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			struct appdata
			{
				float3 positionOS : POSITION;
				float4 uv0 : TEXCOORD0;
				
			};

			struct v2f
			{
				float4 positionCS : SV_POSITION;
				float2 uv0 : TEXCOORD0;				
				
			};

			//CBUFFER_START(UnityPerMaterial)
			float4 _Color;
			StructuredBuffer<int2> GoLData;
			int Width;
			int Height;
			int BitWidth;
			int BitHeight;
			//CBUFFER_END

			

			

			v2f vert(appdata v)
			{
				v2f output = (v2f)0;				

				float3 positionWS = TransformObjectToWorld(v.positionOS);
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv0 = v.uv0.xy;
				return output;
			}

			half4 frag(v2f i) : SV_TARGET
			{
				const int x = floor(i.uv0.x*Width);
				const int y = floor(i.uv0.y*Height);

				const int bitPosX = floor(i.uv0.x*Width*BitWidth)%BitWidth;
				const int bitPosY = floor(i.uv0.y*Height*BitHeight)%BitHeight;
				const int index = x + y*Width;
				const int bitPos = bitPosX + bitPosY*8;
				const bool isLowerInt = bitPos < 32;
				const bool isAlive = isLowerInt ? GoLData[index].x & (1 << bitPos) : GoLData[index].y & (1 << (bitPos-32) );
				
				return isAlive ? _Color : half4(0,0,0,0);
				//return half4((half)bitPosX/Width, (half)bitPosY/Width, 0, 1);
			}

			ENDHLSL
		}
	}
}