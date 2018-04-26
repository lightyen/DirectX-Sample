struct VS_IN
{
    float4 position : POSITION;
    float4 color : COLOR;
    float2 tex : TEXCOORD;
};

struct VS_OUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 tex : TEXCOORD0;
};

float4x4 projection;

VS_OUT main(VS_IN input)
{
    VS_OUT output = (VS_OUT) 0;
    output.position = mul(projection, input.position);
    output.color = input.color;
    output.tex = input.tex;
    return output;
}