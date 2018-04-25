Texture2D tex : register(t0);
SamplerState samLinear : register(s0);

struct VS_OUT
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
};

float4 main(VS_OUT input) : SV_TARGET
{
    //return float4(1, 1, 1, 1) * tex.Sample(samLinear, input.uv);
    return float4(1, 1, 1, 1) * tex.Sample(samLinear, input.uv);
}