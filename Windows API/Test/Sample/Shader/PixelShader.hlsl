// b    Constant Buffer
// t    Texture and Texture Buffer
// c    Buffer Offset
// s    Sampler
// u    Unordered Access View
Texture2D haha : register(t0);
SamplerState samLinear : register(s0);

struct PixelShaderInput {
    float4 position : SV_POSITION;
	float4 color : COLOR;
    float2 tex : TEXCOORD0;
};

float4 main(PixelShaderInput input) : SV_TARGET
{
    float4 t = haha.Sample(samLinear, input.tex);
    return t;
}