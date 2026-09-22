Shader "Hidden/EFTTrainer/HighlightComposite"
{
    HLSLINCLUDE
    #include "UnityCG.cginc"
    sampler2D _TrainerEdge, _TrainerExpanded, _TrainerFill;
    float4 _TrainerTexel;
    int _TrainerRadius;
    float4 horizontal(v2f_img i) : SV_Target
    {
        float4 best = tex2D(_TrainerEdge, i.uv);
        [unroll] for (int x = -5; x <= 5; x++)
        {
            if (abs(x) > _TrainerRadius) continue;
            float4 sample = tex2D(_TrainerEdge, i.uv + float2(x * _TrainerTexel.x, 0));
            if (sample.a > best.a) best = sample;
        }
        return best;
    }
    float4 composite(v2f_img i) : SV_Target
    {
        float4 best = tex2D(_TrainerExpanded, i.uv);
        [unroll] for (int y = -5; y <= 5; y++)
        {
            if (abs(y) > _TrainerRadius) continue;
            float4 sample = tex2D(_TrainerExpanded, i.uv + float2(0, y * _TrainerTexel.y));
            if (sample.a > best.a) best = sample;
        }
        float4 original = tex2D(_TrainerEdge, i.uv);
        float4 fill = tex2D(_TrainerFill, i.uv);
        // The undilated silhouette removes internal seams between body parts/submeshes.
        best.a *= 1 - step(0.001, original.a);
        float alpha = fill.a + best.a * (1 - fill.a);
        return float4((fill.rgb * fill.a + best.rgb * best.a * (1 - fill.a)) / max(alpha, 0.00001), alpha);
    }
    ENDHLSL
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment horizontal
            ENDHLSL
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment composite
            ENDHLSL
        }
    }
}
