// Built-in pipeline. Tints + simple wrap-around lighting + screen-space rect clip.
// _SkinClipRect is set globally by ClipRectFeeder as (xMin, yMin, xMax, yMax) in pixels.
Shader "Prototype/BrushClipped"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _LightDir ("Light Direction", Vector) = (0.4, 0.7, 0.6, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldNormal: TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _LightDir;
            float4 _SkinClipRect;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos   = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Screen-space pixel coordinate of this fragment.
                float2 screenPx = (i.screenPos.xy / max(i.screenPos.w, 1e-6)) * _ScreenParams.xy;

                // Discard anything outside the global clip rect.
                if (screenPx.x < _SkinClipRect.x ||
                    screenPx.x > _SkinClipRect.z ||
                    screenPx.y < _SkinClipRect.y ||
                    screenPx.y > _SkinClipRect.w)
                {
                    discard;
                }

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                fixed4 col = tint * tex;

                float ndl = saturate(dot(normalize(i.worldNormal), normalize(_LightDir.xyz)));
                col.rgb *= 0.5 + 0.5 * ndl;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
