struct VS_IN
{
    float4 position : POSITION;
    float2 uv : TEXCOORD0;
};

struct VS_OUT
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
};

float4x4 projection;

VS_OUT main(VS_IN input)
{
    VS_OUT output = (VS_OUT) 0;
    output.position = mul(projection, input.position);
    output.uv = input.uv;
    return output;
}