//cbuffer CloudConstants  : register(c0)
//{
//	float4x4 world;
//};

struct VS_IN
{
	float4 pos : POSITION;
	float4 color : COLOR;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float4 color : COLOR;
};

PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.pos = input.pos;
	output.uv = input.uv;

	return output;
}

float4 PS(PS_IN input) : SV_Target
{
	return input.color;
}

technique10 Render
{
	pass P0
	{
		SetGeometryShader(0);
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetPixelShader(CompileShader(ps_4_0, PS()));
	}
}