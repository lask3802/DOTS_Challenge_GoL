Shader "Custom/GoLUrpBufferShader"
{
	Properties
	{
		_Color("Color", Color) = (0, 0, 0, 0)		
		[HiddenInInspector]_HighBits("Cells", Integer) = 0
		[HiddenInInspector]_LowBits("Cells", Integer) = 0
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
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			struct appdata
			{
				float3 positionOS : POSITION;
				float4 uv0 : TEXCOORD0;
				#if UNITY_ANY_INSTANCING_ENABLED
				uint instanceID : INSTANCEID_SEMANTIC;
				#endif
			};

			struct v2f
			{
				float4 positionCS : SV_POSITION;
				float2 uv0 : TEXCOORD0;				
				#if UNITY_ANY_INSTANCING_ENABLED
				uint instanceID : CUSTOM_INSTANCE_ID;
				#endif
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color;
			int _HighBits;
			int _LowBits;
			CBUFFER_END

			#if defined(UNITY_DOTS_INSTANCING_ENABLED)
				UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
					UNITY_DOTS_INSTANCED_PROP(float4, _Color)
					UNITY_DOTS_INSTANCED_PROP(int, _HighBits)
					UNITY_DOTS_INSTANCED_PROP(int, _LowBits)
					
				UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

				#define _Color UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Color)
				#define _HighBits UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _HighBits)
				#define _LowBits UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(int, _LowBits)
				
			#endif

			

			v2f vert(appdata v)
			{
				v2f output = (v2f)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, output);

				float3 positionWS = TransformObjectToWorld(v.positionOS);
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv0 = v.uv0.xy;

				#if UNITY_ANY_INSTANCING_ENABLED
				output.instanceID = v.instanceID;
				#endif

				return output;
			}

			half4 frag(v2f i) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(i);
				const bool isLowerInt = i.uv0.y*8 < 4;
				const int bitPos = floor(i.uv0.x*8)+floor(i.uv0.y*8)*8;
				if(isLowerInt)
				{
					return _LowBits.x & (1 << bitPos) ? _Color : half4(0,0,0,0);
				}
				return _HighBits & (1 << (bitPos-32) ) ? _Color : half4(0,0,0,0);
			}

			ENDHLSL
		}
	}
}