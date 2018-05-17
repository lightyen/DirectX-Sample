//--------------------------------------------------------------------------------------
// Constant Buffer Variables
//--------------------------------------------------------------------------------------

cbuffer ConstantBuffer
{
    float4x4 Transform;
}

Texture2D haha : register(t0);
SamplerState samLinear : register(s0);

//--------------------------------------------------------------------------------------

struct VertexShaderInput
{
    float4 position : POSITION;
    float4 color : COLOR;
    float2 tex : TEXCOORD;
};

struct PixelShaderInput
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 tex : TEXCOORD0;
};

//--------------------------------------------------------------------------------------
// Vertex Shader
//--------------------------------------------------------------------------------------

PixelShaderInput VS(VertexShaderInput input)
{
    PixelShaderInput output = (PixelShaderInput) 0;
    output.position = mul(Transform, input.position);
    output.color = input.color;
    output.tex = input.tex;
	return output;
}

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------

float4 PS(PixelShaderInput input) : SV_TARGET
{
    return haha.Sample(samLinear, input.tex);
}
