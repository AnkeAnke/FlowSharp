cbuffer Globals : register(b0)
{
	float4x4 view;
	float4x4 projection;
};

float3 color;
float thickness;

// For shaded heighmapped version.
float3 worldNormal;

Texture2D colormap;
float minMap;
float maxMap;
SamplerState LinSampler {
	filter = MIN_MAG_POINT_MIP_LINEAR;
	AddressU = Clamp;
	AddressV = Clamp;
};
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

struct VS_IN
{
	float3 pos : POSITION;
	float scalar : SCALAR;
};

struct GS_IN
{
	float4 pos : SV_POSITION;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float2 uv : TEXTURE;
	float len : LINE_LENGTH;
};

GS_IN VS(VS_IN input)
{
	GS_IN output = (GS_IN)0;

	output.pos = mul(view, float4(input.pos, 1));

	return output;
}


[maxvertexcount(6)]
void GS(line GS_IN ends[2], inout TriangleStream<PS_IN> triStream)
{
	float2 uv = float2(thickness, thickness);
	float2 uvNeg = float2(thickness, -thickness);

	float4 pos0 = ends[0].pos;
	float4 pos1 = ends[1].pos;
	float2 len = float2(normalize(pos1.xy - pos0.xy) * thickness);
	float2 wid = float2(len.y, -len.x);

	float4 diag = float4(len + wid, 0.0, 0.0);
	float4 diagNeg = float4(-len + wid, 0.0, 0.0);

	float2 lineUV = float2(0, length(pos1.xyz - pos0.xyz));
	PS_IN output = (PS_IN)0;
	float wOffset = 0.000003;

	output.len = lineUV.y;
	output.pos = mul(projection, pos0 - diag); output.pos.w += wOffset;
	output.uv = -uv;
	triStream.Append(output);
	output.pos = mul(projection, pos1 - diagNeg); output.pos.w += wOffset;
	output.uv = lineUV - uvNeg;
	triStream.Append(output);
	output.pos = mul(projection, pos0 + diagNeg); output.pos.w += wOffset;
	output.uv = uvNeg;
	triStream.Append(output);

	output.pos = mul(projection, pos1 - diagNeg); output.pos.w += wOffset;
	output.uv = lineUV - uvNeg;
	triStream.Append(output);
	output.pos = mul(projection, pos0 + diagNeg); output.pos.w += wOffset;
	output.uv = uvNeg;
	triStream.Append(output);
	output.pos = mul(projection, pos1 + diag); output.pos.w += wOffset;
	output.uv = lineUV + uv;
	triStream.Append(output);

}


float4 PS(PS_IN input) : SV_Target
{
	float2 rad;
	rad.x = max(max(input.uv.y - input.len, -input.uv.y), 0.0);
	rad.y = input.uv.x;

	float dist = length(rad);
	if (dist > thickness)
		discard;
	return float4(color * (1.1 - abs(rad.y/thickness) * 0.8), 1.0); // input.color;
}

// ~~~~~~~~~~~~~~~~~~~ Simple Version, Line Output ~~~~~~~~~~~~~~~~~~~~~~~~ //
struct PS_IN_SIMPLE
{
	float4 pos : SV_POSITION;
};

PS_IN_SIMPLE VS_Simple(VS_IN input)
{
	GS_IN output = (PS_IN_SIMPLE)0;

	output.pos = mul(projection, mul(view, input.pos));

	return output;
}

float4 PS_Simple(PS_IN_SIMPLE input) : SV_Target
{
	return float4(color, 1.0); // input.color;
}

// ~~~~~~~~~~~~~~~~~~~ Shaded and Height Color Coded Lines ~~~~~~~~~~~~~~~~~~~~~~~~ //

struct GS_IN_H
{
	float4 pos : SV_POSITION;
	float3 worldPos : WORLD;
	float height : HEIGHT;
};

struct PS_IN_H
{
	float4 pos : SV_POSITION;
	float2 uv : TEXTURE;
	float len : LINE_LENGTH;
	float height : HEIGHT;
	float lightness : LIGHT;
};

GS_IN_H VS_Height(VS_IN input)
{
	GS_IN_H output = (GS_IN_H)0;

	output.pos = mul(view, float4(input.pos, 1));
	output.worldPos = input.pos;
	output.height = input.scalar;
	//output.worldPos.z = 6;

	return output;
}


[maxvertexcount(6)]
void GS_Height(line GS_IN_H ends[2], inout TriangleStream<PS_IN_H> triStream)
{
	float2 uv = float2(thickness, thickness);
	float2 uvNeg = float2(thickness, -thickness);

	float4 pos0 = ends[0].pos;
	float4 pos1 = ends[1].pos;
	float2 len = float2(normalize(pos1.xy - pos0.xy) * thickness);
	float2 wid = float2(len.y, -len.x);

	float angle = length(cross(normalize(ends[1].worldPos.xyz - ends[0].worldPos.xyz), worldNormal));
	float light = 1 - angle;
	float height0 = (ends[0].height - minMap) / (maxMap - minMap);
	float height1 = (ends[1].height - minMap) / (maxMap - minMap);

	float4 diag = float4(len + wid, 0.0, 0.0);
	float4 diagNeg = float4(-len + wid, 0.0, 0.0);

	float2 lineUV = float2(0, length(pos1.xyz - pos0.xyz));
	PS_IN_H output = (PS_IN_H)0;
	float wOffset = 0.000003;

	output.len = lineUV.y;
	output.pos = mul(projection, pos0 - diag); output.pos.w += wOffset;
	output.uv = -uv;
	output.height = height0;
	output.lightness = light;
	triStream.Append(output);
	output.pos = mul(projection, pos1 - diagNeg); output.pos.w += wOffset;
	output.uv = lineUV - uvNeg;
	output.height = height1;
	triStream.Append(output);
	output.pos = mul(projection, pos0 + diagNeg); output.pos.w += wOffset;
	output.uv = uvNeg;
	output.height = height0;
	triStream.Append(output);

	output.pos = mul(projection, pos1 - diagNeg); output.pos.w += wOffset;
	output.uv = lineUV - uvNeg;
	output.height = height1;
	triStream.Append(output);
	output.pos = mul(projection, pos0 + diagNeg); output.pos.w += wOffset;
	output.uv = uvNeg;
	output.height = height0;
	triStream.Append(output);
	output.pos = mul(projection, pos1 + diag); output.pos.w += wOffset;
	output.uv = lineUV + uv;
	output.height = height1;
	triStream.Append(output);

}


float4 PS_Height(PS_IN_H input) : SV_Target
{
	float2 rad;
	rad.x = max(max(input.uv.y - input.len, -input.uv.y), 0.0);
	rad.y = input.uv.x;

	float dist = length(rad);
	if (dist > thickness)
		discard;
	return float4(colormap.Sample(LinSampler, float2(input.height, 0.5)).xyz, 1.0); // input.color;
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

technique10 Height
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS_Height()));
		SetGeometryShader(CompileShader(gs_4_0, GS_Height()));
		SetPixelShader(CompileShader(ps_4_0, PS_Height()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}

technique10 Simple
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS_Simple()));
		SetGeometryShader(0);
		SetPixelShader(CompileShader(ps_4_0, PS_Simple()));
		SetBlendState(SrcAlphaBlendingAdd, float4(0.0f, 0.0f, 0.0f, 0.0f), 0xFFFFFFFF);
	}
}