// Intentionally has no ShadowCaster pass. Visibility must use the real scene depth,
// including alpha-test holes, instead of an approximate camera depth texture.
Shader "Hidden/TrainerChecks/Fence"
{
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying vert(Input v) { Varying o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(Varying i):SV_Target
            {
                float2 grid = frac(i.uv * 8);
                clip(0.2 - min(grid.x, grid.y));
                return fixed4(0.1, 0.8, 0.1, 1);
            }
            ENDCG
        }
    }
}
