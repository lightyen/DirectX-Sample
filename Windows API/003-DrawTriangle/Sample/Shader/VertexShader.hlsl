struct VertexShaderInput
{
    float4 position : POSITION;
    float4 color : COLOR;
};

struct PixelShaderInput
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

PixelShaderInput main(VertexShaderInput input)
{
    PixelShaderInput output = (PixelShaderInput) 0;
    output.position = input.position;
    output.color = input.color;
	return output;
}