cbuffer Globals : register(b0)
{
	float4x4 view;
	float4x4 projection;
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

	output.pos = mul(view, input.pos);
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
	PS_IN output = (PS_IN)0;
	output.color = center[0].color;
	float wOffset = 0.000005;

	output.pos = mul(projection, float4(pos - diag, 1)); output.pos.w += wOffset;
	output.uv = float2(-1, -1);
	triStream.Append(output);
	output.pos = mul(projection, float4(pos - diagNeg, 1)); output.pos.w += wOffset;
	output.uv = float2(-1, 1);
	triStream.Append(output);
	output.pos = mul(projection, float4(pos + diagNeg, 1)); output.pos.w += wOffset;
	output.uv = float2(1, -1);
	triStream.Append(output);

	output.pos = mul(projection, float4(pos - diagNeg, 1)); output.pos.w += wOffset;
	output.uv = float2(-1, 1);
	triStream.Append(output);
	output.pos = mul(projection, float4(pos + diagNeg, 1)); output.pos.w += wOffset;
	output.uv = float2(1, -1);
	triStream.Append(output);
	output.pos = mul(projection, float4(pos + diag, 1)); output.pos.w += wOffset;
	output.uv = float2(1, 1);
	triStream.Append(output);
	
}


float4 PS(PS_IN input) : SV_Target
{
	float len = length(input.uv);
	if (len >= 1.0)
		discard;
//input.pos.w = -2.0;
	return float4(input.color * (1.1 - len*0.8), 1.0); // input.color;
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