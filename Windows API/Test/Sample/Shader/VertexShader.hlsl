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

PixelShaderInput main(VertexShaderInput input)
{
    PixelShaderInput output = (PixelShaderInput) 0;
    output.position = input.position;
    output.color = input.color;
    output.tex = input.tex;
	return output;
}