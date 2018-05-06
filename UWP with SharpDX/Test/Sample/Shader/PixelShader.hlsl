Texture2D texture0 : register(t0);
SamplerState samLinear : register(s0);

struct VS_OUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 tex : TEXCOORD0;
};

float4 main(VS_OUT input) : SV_TARGET
{
    return texture0.Sample(samLinear, input.tex);
}