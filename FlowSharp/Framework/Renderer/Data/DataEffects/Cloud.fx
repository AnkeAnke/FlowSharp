cbuffer FieldConstants : register(c0)
{
	//float radius;
};

struct VS_IN
{
	float4 pos : POSITION;
	float3 color : COLOR;
	float radius : RADIUS;
};

struct GS_IN
{
	float4 pos : SV_POSITION;
	float3 color : COLOR;
	float radius : RADIUS;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float3 color : COLOR;
	float2 uv : TEXTURE;
};

GS_IN VS(VS_IN input)
{
	GS_IN output = (GS_IN)0;

	output.pos = input.pos;
	output.color = input.color;
	output.radius = input.radius;

	return output;
}


[maxvertexcount(6)]
void GS(point GS_IN center[1], inout TriangleStream<PS_IN> triStream)
{
	float radius = center[0].radius;
	float3 diag = float3(radius, radius, 0.0);
	float3 diagNeg = float3(radius, -radius, 0.0);
	float3 pos = center[0].pos.xyz;
	pos.z -= 0.01;
	PS_IN output = (PS_IN)0;
	output.color = center[0].color;
	

	output.pos = float4(pos - diag, 1);
	output.uv = float2(-1, -1);
	triStream.Append(output);
	output.pos = float4(pos - diagNeg, 1);
	output.uv = float2(-1, 1);
	triStream.Append(output);
	output.pos = float4(pos + diagNeg, 1);
	output.uv = float2(1, -1);
	triStream.Append(output);

	output.pos = float4(pos - diagNeg, 1);
	output.uv = float2(-1, 1);
	triStream.Append(output);
	output.pos = float4(pos + diagNeg, 1);
	output.uv = float2(1, -1);
	triStream.Append(output);
	output.pos = float4(pos + diag, 1);
	output.uv = float2(1, 1);
	triStream.Append(output);
	
}


float4 PS(PS_IN input) : SV_Target
{
	return length(input.uv) <= 1.0 ? float4(input.color, 1.0) : float4(0,0,0,0); // input.color;
}

BlendState SrcAlphaBlendingAdd
{
	BlendEnable[0] = TRUE;
	SrcBlend = SRC_ALPHA;
	DestBlend = INV_SRC_ALPHA;
	BlendOp = ADD;
	SrcBlendAlpha = ONE;
	DestBlendAlpha = ONE;
	BlendOpAlpha = ADD;
	RenderTargetWriteMask[0] = 0x0F;
};

technique10 Render
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetGeometryShader(CompileShader(gs_4_0, GS()));
		SetPixelShader(CompileShader(ps_4_0, PS()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}