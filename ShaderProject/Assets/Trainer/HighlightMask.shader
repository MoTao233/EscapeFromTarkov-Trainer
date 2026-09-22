Shader "Hidden/EFTTrainer/HighlightMask"
{
    HLSLINCLUDE
    #include "UnityCG.cginc"
    sampler2D _TrainerAlphaTex;
    float4 _TrainerAlphaST;
    float _TrainerCutoff;
    float4 _TrainerFillColor, _TrainerEdgeColor, _TrainerHiddenColor;
    struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct Varying { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    struct Output { float4 fill : SV_Target0; float4 edge : SV_Target1; };
    Varying vert(Input v)
    {
        Varying o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv * _TrainerAlphaST.xy + _TrainerAlphaST.zw;
        return o;
    }
    Output Mask(Varying i, float4 color)
    {
        if (_TrainerCutoff > 0) clip(tex2D(_TrainerAlphaTex, i.uv).a - _TrainerCutoff);
        Output o;
        o.fill = color;
        o.edge = _TrainerEdgeColor;
        return o;
    }
    Output visible(Varying i) { return Mask(i, _TrainerFillColor); }
    Output hidden(Varying i) { return Mask(i, _TrainerHiddenColor); }
    ENDHLSL
    SubShader
    {
        Cull Back ZWrite Off
        Pass
        {
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment visible
            ENDHLSL
        }
        Pass
        {
            ZTest Greater
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment hidden
            ENDHLSL
        }
    }
}
